using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Chat;

public sealed record ChatTrace(string Kind, string Text, string? ToolName = null, JObject? Arguments = null);

/// <summary>Bounded model/tool loop. Finished and interrupted turns retain intent and known receipts.</summary>
public sealed class ChatSession(IAiProvider provider, IMcpClient mcp)
{
    private readonly Queue<IReadOnlyList<AiMessage>> turns = new();
    private readonly object historyGate = new();
    private readonly SemaphoreSlim sendGate = new(1, 1);
    public event Action<ChatTrace>? Trace;
    public bool LastResponseUsedModel { get; private set; }
    public Func<JObject, CancellationToken, Task<bool>>? ConfirmChangeAsync { get; set; }
    public Func<UserQuestion, CancellationToken, Task<QuestionAnswer?>>? AskUserAsync { get; set; }
    private string responseLanguage = "auto";
    public string ResponseLanguage { get => responseLanguage; set => responseLanguage = ResponseLanguages.Normalize(value); }
    public bool DeveloperMode { get; set; }

    public void Clear()
    {
        lock (historyGate) turns.Clear();
    }

    public void RestoreConversation(IReadOnlyList<IReadOnlyList<AiMessage>> history)
    {
        Clear();
        foreach (var turn in history) Remember(ModelHistory.Portable(turn));
    }

    /// <summary>
    /// Returns finished or interrupted turns. The active turn is absent until
    /// it finishes or fails, while the Studio's
    /// diagnostic trace records an interrupted active turn immediately.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<AiMessage>> GetConversationSnapshot()
    {
        lock (historyGate)
        {
            return turns
                .Select(turn => (IReadOnlyList<AiMessage>)turn.Select(CloneMessage).ToArray())
                .ToArray();
        }
    }

    public Task<string> SendAsync(string text, CancellationToken cancellationToken) => SendAsync(text, [], cancellationToken);

    public async Task<string> SendAsync(string text, IReadOnlyList<ChatAttachment> attachments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(attachments);
        if (string.IsNullOrWhiteSpace(text) && attachments.Count == 0) throw new ArgumentException(StudioStrings.Get("Chat.EnterMessage"), nameof(text));
        if (text.Length > 32000) throw new ArgumentException(StudioStrings.Get("Chat.MessageLimit"), nameof(text));
        var turnResponseLanguage = ResponseLanguage;
        var turnDeveloperMode = DeveloperMode;
        var userMessage = ChatAttachments.CreateUserMessage(text, attachments);
        await sendGate.WaitAsync(cancellationToken);
        LastResponseUsedModel = false;
        List<AiMessage>? interruptedMessages = null;
        var interruptedStart = 0;
        var changeAttempted = false;
        var questionAnswered = false;
        var pendingQuestionImages = new List<ChatAttachment>();
        try
        {
            IReadOnlyList<AiMessage>? previous;
            lock (historyGate) previous = turns.LastOrDefault();
            var sortedTurn = attachments.Count == 0 ? await PdmSortRequest.Run(text, previous, mcp, trace => Trace?.Invoke(trace), cancellationToken) : null;
            if (sortedTurn != null)
            {
                Remember(sortedTurn);
                return sortedTurn[^1].Content;
            }
            var tools = mcp.IsConnected ? mcp.Tools.ToArray() : [];
            if (tools.Any(t => t.Name == QuestionSources.ToolName)) throw new InvalidOperationException("MCP tool conflicts with Studio's question dialog.");
            var questionSources = new QuestionSources();
            var questionsAsked = 0;
            var persistenceRequest = attachments.Count == 0 ? PdmPersistenceRequest.Parse(text) : null;
            var creationRequest = attachments.Count == 0 && persistenceRequest == null ? PdmCreateRequest.Parse(text) : null;
            var creationPlan = persistenceRequest != null ? await persistenceRequest.Resolve(mcp, trace => Trace?.Invoke(trace), cancellationToken, AskUserAsync)
                : creationRequest == null ? null : await creationRequest.Resolve(mcp, trace => Trace?.Invoke(trace), cancellationToken, AskUserAsync);
            if (creationPlan?.Answer != null) {
                Remember([new AiMessage { Role = "user", Content = text }, new AiMessage { Role = "assistant", Content = creationPlan.Answer }]);
                return creationPlan.Answer;
            }
            // Retry/clarification turns retain the original workflow's schemas.
            // This is local selection context, not extra provider payload or authority.
            string workflowContext;
            lock (historyGate) workflowContext = string.Join("\n", turns.TakeLast(8).SelectMany(t => t.Where(m => m.Role == "user").Select(m => m.UserIntent ?? m.Content)));
            var exposure = new ToolExposure(tools, text + "\n" + workflowContext, compact: provider is OllamaProvider, currentRequest: text);
            var listNames = attachments.Count == 0 ? PdmListRequest.Tools(text) : [];
            if (listNames.Any(name => !tools.Any(t => t.Name == name && !t.RequiresConfirmation))) listNames = [];
            var inventory = new PdmInventory(listNames.Length > 0 ? "list all " + text : "");
            var messages = new List<AiMessage>
            {
                new()
                {
                    Role = "system",
                    Content = ModelInstructions.Build(exposure.Active, exposure.Catalog, tools.Length > 0, turnResponseLanguage, turnDeveloperMode)
                }
            };
            IReadOnlyList<IReadOnlyList<AiMessage>> history;
            lock (historyGate) history = turns.ToArray();
            if (listNames.Length == 0 && creationPlan == null) foreach (var turn in history) messages.AddRange(ModelHistory.ForTurn(turn));
            else if (listNames.Length > 0) messages[0].Content = "Call the supplied read-only TopSolid list tools for the user's requested categories, together in one response, with limit=100 and offset=0. Do not guess names. Studio renders the complete list from tool receipts; do not write or summarize the list yourself. " + ResponseLanguages.Instruction(turnResponseLanguage);
            var start = messages.Count;
            interruptedMessages = messages;
            interruptedStart = start;
            messages.Add(userMessage);
            var hasAttachments = messages.Any(m => m.Images.Count > 0 || (m.UserIntent != null && m.UserIntent != m.Content));
            var callsExecuted = 0;
            if (creationPlan == null && listNames.Length == 0 && ActiveDocumentContext.Requested(text) && tools.Any(t => t.Name == ActiveDocumentContext.Tool && !t.RequiresConfirmation)) {
                var activeContext = await ActiveDocumentContext.Fetch(mcp, trace => Trace?.Invoke(trace), cancellationToken);
                messages.AddRange(activeContext);
                foreach (var receipt in activeContext.Where(m => m.Role == "tool"))
                {
                    questionSources.Capture(receipt.ToolCallId!, receipt.ToolName!, new JObject(), JsonConvert.DeserializeObject<McpToolResult>(receipt.Content)!);
                    if (AskUserAsync != null) receipt.Content = questionSources.ExposeSource(receipt.ToolCallId!, receipt.Content);
                }
                callsExecuted++;
            }
            var furtherChangesBlocked = false;
            var invalidProposals = 0;
            var submittedChanges = new List<(string Name, JObject Arguments)>();
            for (var round = 0; round < 16; round++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var exposedTools = creationPlan?.Call != null ? tools.Where(t => t.Name == creationPlan.Call.Name).ToArray() : listNames.Length == 0 ? exposure.Active : tools.Where(t => listNames.Contains(t.Name)).ToArray();
                if (AskUserAsync != null && creationPlan == null && listNames.Length == 0) exposedTools = [.. exposedTools, QuestionSources.Definition];
                AiReply reply;
                if (creationPlan?.Call != null) {
                    Trace?.Invoke(new ChatTrace("PDM", "Explicit PDM action uses verified MCP scope and the normal confirmation path; no model inference required."));
                    reply = new AiReply { ToolCalls = [creationPlan.Call] };
                } else {
                    LastResponseUsedModel = true;
                    if (listNames.Length == 0) messages[0].Content = ModelInstructions.Build(exposedTools, exposure.Catalog, tools.Length > 0, turnResponseLanguage, turnDeveloperMode);
                    if (exposedTools.Any(t => t.Name == QuestionSources.ToolName)) messages[0].Content += "\n" + QuestionSources.Instructions;
                    if (hasAttachments) messages[0].Content += "\n" + ChatAttachments.ModelBoundary;
                    Trace?.Invoke(new ChatTrace("Model", $"Request {round + 1}; {tools.Length} MCP tools discovered, {exposedTools.Length} tool schemas supplied; input {messages.Sum(MessageSize):N0} message characters + {exposedTools.Sum(t => t.InputSchema.ToString(Formatting.None).Length + t.Description.Length):N0} schema characters; provider={provider.GetType().Name}."));
                    var modelTimer = System.Diagnostics.Stopwatch.StartNew();
                    try { reply = await provider.CompleteAsync(messages, exposedTools, cancellationToken);
                        if (reply.Metrics != null) Trace?.Invoke(new ChatTrace("Model metrics", reply.Metrics.ToString(Formatting.None))); }
                    finally { Trace?.Invoke(new ChatTrace("Timing", $"Model request {round + 1}: {modelTimer.Elapsed.TotalSeconds:F2} s")); }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (reply.ToolCalls.Count > 24 - callsExecuted)
                    throw new InvalidOperationException("The model exceeded the 24-tool-call limit. Start a shorter request.");
                messages.Add(new AiMessage { Role = "assistant", Content = reply.Content, ToolCalls = reply.ToolCalls, Thinking = reply.Thinking, ProviderContent = reply.ProviderContent, ExtraContent = reply.ExtraContent });
                if (reply.ToolCalls.Count == 0)
                {
                    if (listNames.Length > 0 && !listNames.All(inventory.HasCategory))
                        throw new InvalidOperationException("The model did not query all requested PDM lists. No unverified names are displayed. Try the request again.");
                    if (string.IsNullOrWhiteSpace(reply.Content)) throw new InvalidOperationException("The model returned an empty answer.");
                    while (callsExecuted < 24 && inventory.NextPage() is { } page)
                    {
                        if (!tools.Any(t => t.Name == page.Tool && !t.RequiresConfirmation)) break;
                        var arguments = new JObject { ["offset"] = page.Offset, ["limit"] = 100 };
                        Trace?.Invoke(new ChatTrace("Tool call", page.Tool + " " + arguments.ToString(Formatting.None)));
                        var receipt = await mcp.CallToolAsync(page.Tool, arguments, cancellationToken);
                        callsExecuted++;
                        Trace?.Invoke(new ChatTrace(receipt.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(receipt)));
                        inventory.Capture(page.Tool, receipt);
                        if (receipt.IsError || inventory.NextPage() == page) break;
                    }
                    var answer = inventory.Render() ?? reply.Content;
                    messages[^1] = new AiMessage { Role = "assistant", Content = answer };
                    Remember(messages.Skip(start).ToArray());
                    return answer;
                }
                var ids = new HashSet<string>(StringComparer.Ordinal);
                if (reply.ToolCalls.Any(call => string.IsNullOrWhiteSpace(call.Id) || !ids.Add(call.Id)))
                    throw new InvalidOperationException("The model returned a missing or duplicate tool call ID.");
                foreach (var call in reply.ToolCalls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var toolTimer = System.Diagnostics.Stopwatch.StartNew();
                    var confirmationSeconds = 0.0;
                    callsExecuted++;
                    Trace?.Invoke(new ChatTrace("Tool call", call.Name + " " + call.Arguments.ToString(Formatting.None)));
                    McpToolResult result;
                    var declined = false;
                    var questionCancelled = false;
                    var invalidProposal = false;
                    if (!string.IsNullOrEmpty(call.ArgumentsError))
                    {
                        invalidProposal = true;
                        result = McpToolResult.Error("Invalid tool arguments: " + call.ArgumentsError);
                    }
                    else if (call.Name == QuestionSources.ToolName && AskUserAsync != null && exposedTools.Any(t => t.Name == call.Name))
                    {
                        UserQuestion? question = null;
                        string? questionError = null;
                        try
                        {
                            if (++questionsAsked > 8) throw new ArgumentException("Question limit reached. Continue with the answers already provided.");
                            question = questionSources.Create(call.Arguments);
                        }
                        catch (ArgumentException error) { invalidProposal = true; questionError = error.Message; }
                        if (question == null) result = McpToolResult.Error(questionError ?? "Invalid question. Read current named choices before retrying.");
                        else
                        {
                            var questionTimer = System.Diagnostics.Stopwatch.StartNew();
                            var answer = await AskUserAsync(question, cancellationToken);
                            confirmationSeconds = questionTimer.Elapsed.TotalSeconds;
                            cancellationToken.ThrowIfCancellationRequested();
                            questionCancelled = answer == null;
                            questionAnswered |= answer != null;
                            result = new McpToolResult { StructuredContent = answer?.Data ?? new JObject { ["status"] = "cancelled", ["message"] = "User cancelled the question; stop this workflow without further actions." } };
                            if (answer?.Image is { } image) { pendingQuestionImages.Add(image); hasAttachments = true; }
                        }
                    }
                    else if (exposure.IsSelector(call.Name))
                        result = exposure.Select(call.Arguments);
                    else if (!exposedTools.Any(t => string.Equals(t.Name, call.Name, StringComparison.Ordinal)))
                        result = tools.Any(t => t.Name == call.Name)
                            ? exposure.Select(new JObject { ["names"] = new JArray(call.Name) })
                            : McpToolResult.Error("Unknown tool. Only call tools from the supplied discovered list.");
                    else
                    {
                        var definition = tools.Single(t => t.Name == call.Name);
                        if (!definition.RequiresConfirmation) result = await mcp.CallToolAsync(call.Name, call.Arguments, cancellationToken);
                        else if (reply.ToolCalls.Any(t => t.Name == QuestionSources.ToolName))
                            result = McpToolResult.Error("Do not combine a question and a CAD change in one batch. Use the answer on the next model round and obtain normal change confirmation.");
                        else if (furtherChangesBlocked || submittedChanges.Any(c => c.Name == call.Name && JToken.DeepEquals(c.Arguments, call.Arguments)))
                            result = McpToolResult.Error("Further or repeated changes are blocked for this turn. Ask the user to review the prior result and give a new instruction.");
                        else if (mcp is not IConfirmableMcpClient confirmable || ConfirmChangeAsync == null)
                            result = McpToolResult.Error("This client cannot obtain explicit user confirmation. No change was made.");
                        else
                        {
                            JObject? proposal = null;
                            McpToolResult? preparationFailure = null;
                            try { proposal = await confirmable.PrepareToolAsync(call.Name, call.Arguments, cancellationToken); }
                            catch (StdioMcpClient.McpRequestException ex) { invalidProposal = true; preparationFailure = McpToolResult.Error("The change was not prepared or executed: " + ex.Message); }
                            if (proposal == null) result = preparationFailure ?? throw new InvalidOperationException("MCP returned no change preview. No change was sent.");
                            else
                            {
                                if ((string?)proposal["toolName"] != call.Name || !JToken.DeepEquals(proposal["arguments"], call.Arguments) ||
                                    proposal["target"] is not JObject || string.IsNullOrEmpty((string?)proposal["confirmationToken"]))
                                    throw new InvalidOperationException("Invalid change preview from MCP. No change was sent.");
                                var visibleProposal = (JObject)proposal.DeepClone();
                                visibleProposal.Remove("confirmationToken");
                                var confirmationTimer = System.Diagnostics.Stopwatch.StartNew();
                                var approved = await ConfirmChangeAsync(visibleProposal, cancellationToken);
                                confirmationSeconds = confirmationTimer.Elapsed.TotalSeconds;
                                if (!approved)
                                {
                                    declined = true;
                                    furtherChangesBlocked = true;
                                    result = McpToolResult.Error("User declined this change. No change was made. Do not ask again unless the user requests it.");
                                }
                                else
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    changeAttempted = true;
                                    submittedChanges.Add((call.Name, (JObject)call.Arguments.DeepClone()));
                                    result = await confirmable.CallConfirmedToolAsync(call.Name, call.Arguments, (string)proposal["confirmationToken"]!, cancellationToken);
                                    furtherChangesBlocked = result.IsError;
                                    Trace?.Invoke(new ChatTrace("CAD change", JsonConvert.SerializeObject(result), call.Name, (JObject)call.Arguments.DeepClone()));
                                }
                            }
                        }
                    }
                    inventory.Capture(call.Name, result);
                    questionSources.Capture(call.Id, call.Name, call.Arguments, result);
                    var content = ToolResultContext.Serialize(result);
                    if (AskUserAsync != null) content = questionSources.ExposeSource(call.Id, content);
                    if (content.Length > 64000)
                        content = JsonConvert.SerializeObject(McpToolResult.Error("Tool output exceeded the 64,000-character limit."));
                    Trace?.Invoke(new ChatTrace(result.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(result), call.Name, (JObject)call.Arguments.DeepClone()));
                    Trace?.Invoke(new ChatTrace("Timing", $"Tool {call.Name}: {Math.Max(0, toolTimer.Elapsed.TotalSeconds - confirmationSeconds):F2} s; user confirmation: {confirmationSeconds:F2} s"));
                    messages.Add(new AiMessage { Role = "tool", Content = content, ToolCallId = call.Id, ToolName = call.Name });
                    if (invalidProposal && ++invalidProposals >= 3)
                        throw new InvalidOperationException("Stopped after three invalid tool proposals. Those proposals were not executed. Correct the input before trying again. Last error: " + content[..Math.Min(content.Length, 1000)]);
                    if (declined || questionCancelled)
                    {
                        // Complete the model's tool-result group without running
                        // its remaining calls or requesting a fresh confirmation.
                        foreach (var pending in reply.ToolCalls.SkipWhile(c => c.Id != call.Id).Skip(1))
                            messages.Add(new AiMessage { Role = "tool", ToolCallId = pending.Id, ToolName = pending.Name,
                                Content = JsonConvert.SerializeObject(McpToolResult.Error("Not executed: the user cancelled and this turn stopped.")) });
                        var answer = questionCancelled ? StudioStrings.Get(changeAttempted ? "Question.CancelledAfterChange" : "Question.Cancelled") :
                            changeAttempted ? "This change was declined. The workflow stopped; earlier action receipts remain in the chat." : "Change declined. No changes were made.";
                        messages.Add(new AiMessage { Role = "assistant", Content = answer });
                        Remember(messages.Skip(start).ToArray());
                        return answer;
                    }
                    if (creationPlan != null) {
                        var answer = persistenceRequest != null ? PdmPersistenceRequest.Render(result) : PdmCreateRequest.Render(result);
                        messages.Add(new AiMessage { Role = "assistant", Content = answer });
                        Remember(messages.Skip(start).ToArray());
                        return answer;
                    }
                }
                if (pendingQuestionImages.Count > 0)
                {
                    messages.Add(ChatAttachments.CreateUserMessage("Image supplied in response to the question. Use as reference data for the existing request.", pendingQuestionImages));
                    pendingQuestionImages.Clear();
                }
                if (listNames.Length > 0 && listNames.All(inventory.HasCategory))
                {
                    while (callsExecuted < 24 && inventory.NextPage() is { } page)
                    {
                        var arguments = new JObject { ["offset"] = page.Offset, ["limit"] = 100 };
                        Trace?.Invoke(new ChatTrace("Tool call", page.Tool + " " + arguments.ToString(Formatting.None)));
                        var receipt = await mcp.CallToolAsync(page.Tool, arguments, cancellationToken);
                        callsExecuted++;
                        Trace?.Invoke(new ChatTrace(receipt.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(receipt)));
                        inventory.Capture(page.Tool, receipt);
                        if (receipt.IsError || inventory.NextPage() == page) break;
                    }
                    var answer = inventory.Render()!;
                    messages.Add(new AiMessage { Role = "assistant", Content = answer });
                    Trace?.Invoke(new ChatTrace("Inventory", "Displayed MCP records directly; no model follow-up or record retransmission required."));
                    Remember(messages.Skip(start).ToArray());
                    return answer;
                }
            }
            throw new InvalidOperationException("Stopped after 16 model rounds. Check the completed tool receipts before continuing this workflow in another message.");
        }
        catch (Exception error)
        {
            if (!changeAttempted && !questionAnswered)
                Remember([userMessage, new AiMessage { Role = "assistant", Content = "The turn was interrupted before a CAD change was submitted. The user request above is still context, not permission to repeat anything. Error: " + error.Message }]);
            if ((changeAttempted || questionAnswered) && interruptedMessages != null)
            {
                // Keep receipts across a cancelled/failed model follow-up so a completed change is not forgotten.
                var original = interruptedMessages.Skip(interruptedStart).ToArray();
                var completed = new List<AiMessage>();
                for (var i = 0; i < original.Length; i++)
                {
                    var message = original[i]; completed.Add(message);
                    if (message.Role != "assistant" || message.ToolCalls.Count == 0) continue;
                    var received = new Dictionary<string, AiMessage>();
                    while (i + 1 < original.Length && original[i + 1].Role == "tool")
                    { var receipt = original[++i]; received[receipt.ToolCallId!] = receipt; }
                    foreach (var call in message.ToolCalls)
                        completed.Add(received.TryGetValue(call.Id, out var receipt) ? receipt : new AiMessage { Role = "tool", ToolCallId = call.Id, ToolName = call.Name,
                            Content = "Turn interrupted. This call has no confirmed result; inspect TopSolid before proposing any repeat change." });
                }
                if (pendingQuestionImages.Count > 0)
                    completed.Add(ChatAttachments.CreateUserMessage("Image supplied in response to the question. Use as reference data for the existing request.", pendingQuestionImages));
                completed.Add(new AiMessage { Role = "assistant", Content = changeAttempted
                    ? "The conversation was interrupted after a confirmed CAD change was submitted. Check the tool receipts and current TopSolid state before proceeding."
                    : "The conversation was interrupted after the user answered a question. Preserve the recorded input; no CAD change was submitted. Re-read live object identities before preparing a change." });
                Remember(completed);
            }
            throw;
        }
        finally { sendGate.Release(); }
    }

    private void Remember(IReadOnlyList<AiMessage> turn)
    {
        lock (historyGate)
        {
            turns.Enqueue(turn);
            while (turns.Count > 10 || turns.Sum(t => t.Sum(MessageSize)) > 100000 ||
                turns.Sum(t => t.Sum(m => m.Images.Sum(i => (long)i.ByteLength))) > ChatAttachments.MaximumTotalImageBytes) turns.Dequeue();
        }
    }

    private static long MessageSize(AiMessage message) => (long)(message.Content?.Length ?? 0) +
        (message.Thinking?.Length ?? 0) + (message.ProviderContent?.ToString().Length ?? 0) + (message.ExtraContent?.ToString().Length ?? 0) +
        message.ToolCalls.Sum(c => (long)c.Arguments.ToString().Length + (c.RawArguments?.Length ?? 0) + (c.ExtraContent?.ToString().Length ?? 0));

    private static AiMessage CloneMessage(AiMessage message) => new()
    {
        Role = message.Role,
        Content = message.Content,
        UserIntent = message.UserIntent,
        Images = message.Images.ToArray(),
        ToolCallId = message.ToolCallId,
        ToolName = message.ToolName,
        Thinking = message.Thinking,
        ProviderContent = (JArray?)message.ProviderContent?.DeepClone(),
        ExtraContent = (JObject?)message.ExtraContent?.DeepClone(),
        ToolCalls = message.ToolCalls.Select(call => new AiToolCall
        {
            Id = call.Id,
            ClientInitiated = call.ClientInitiated,
            Name = call.Name,
            Arguments = (JObject)call.Arguments.DeepClone(),
            ExtraContent = (JObject?)call.ExtraContent?.DeepClone(),
            ArgumentsError = call.ArgumentsError,
            RawArguments = call.RawArguments
        }).ToArray()
    };
}
