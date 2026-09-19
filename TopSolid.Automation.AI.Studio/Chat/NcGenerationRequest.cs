using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

/// <summary>Deterministic NC workflow: operation selection, configured PP selection, generation, then export.</summary>
internal static class NcGenerationRequest
{
    internal const string OperationTool = "topsolid_list_cam_operation_summaries";
    internal const string PostProcessorTool = "topsolid_get_postprocessor_id";
    internal const string GenerateTool = "topsolid_generate_nc_for_selection";
    internal const string ExportTool = "topsolid_export_nc_file";

    internal static bool Matches(string text)
    {
        bool Has(string pattern) => System.Text.RegularExpressions.Regex.IsMatch(text, pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return Has(@"\bNC\b|NC\s*(?:코드|파일)|(?:엔씨|수치제어)") &&
            Has(@"생성|출력|만들|뽑|내보내|저장|generate|create|export|output|post[- ]?process") &&
            !Has(@"목록|리스트|조회|보여|list|show|inspect|read") ||
            Has(@"generate|create|export|output|post[- ]?process") && Has(@"\bNC\b");
    }

    internal static async Task<IReadOnlyList<AiMessage>?> Run(string text, IMcpClient mcp,
        Func<UserQuestion, CancellationToken, Task<QuestionAnswer?>>? ask,
        Func<JObject, CancellationToken, Task<bool>>? confirm,
        Func<JObject, CancellationToken, Task<string?>>? chooseDestination,
        Action<ChatTrace> trace, CancellationToken token)
    {
        if (!Matches(text)) return null;
        var turn = new List<AiMessage> { new() { Role = "user", Content = text } };
        IReadOnlyList<AiMessage> Finish(string key, params object[] args)
        { turn.Add(new AiMessage { Role = "assistant", Content = StudioStrings.Get(key, args) }); return turn; }

        if (ask == null || confirm == null || chooseDestination == null || !mcp.IsConnected ||
            !HasTool(mcp, OperationTool, false) || !HasTool(mcp, PostProcessorTool, false) ||
            !HasTool(mcp, GenerateTool, true) || !HasTool(mcp, ExportTool, true))
            return Finish("Nc.Unavailable");

        var sources = new QuestionSources();
        var references = new JArray();
        var rows = new List<JObject>();
        string? document = null;
        int? total = null;
        var offset = 0;
        for (var page = 0; page < 24; page++)
        {
            token.ThrowIfCancellationRequested();
            var arguments = new JObject { ["offset"] = offset, ["limit"] = 20 };
            if (document != null) arguments["documentId"] = document;
            var callId = "nc-operations-" + Guid.NewGuid().ToString("N");
            var result = await ReadTool(mcp, OperationTool, arguments, callId, turn, trace, token);
            var data = PdmInventory.Data(result);
            if (result.IsError || data?["items"] is not JArray items || data["total"]?.Type != JTokenType.Integer ||
                data["offset"]?.Type != JTokenType.Integer || (int?)data["offset"] != offset || data["hasMore"]?.Type != JTokenType.Boolean)
                return Finish("Nc.OperationUnavailable");
            var pageTotal = (int)data["total"]!;
            var next = offset + items.Count;
            var more = (bool)data["hasMore"]!;
            if (pageTotal < 0 || total.HasValue && total != pageTotal || next > pageTotal ||
                more != (next < pageTotal) || more && (items.Count == 0 || (int?)data["nextOffset"] != next))
                return Finish("Nc.OperationIncomplete");
            total = pageTotal;
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index] is not JObject row || row["operation"] is not JObject operation)
                    return Finish("Nc.OperationIncomplete");
                var identity = operation["element"] as JObject;
                var rowDocument = (string?)identity?["documentId"];
                if (identity == null || string.IsNullOrWhiteSpace(rowDocument) || identity["id"] == null ||
                    document != null && document != rowDocument || rows.Any(existing => JToken.DeepEquals(existing["operation"], operation)))
                    return Finish("Nc.OperationIncomplete");
                document ??= rowDocument;
                rows.Add((JObject)row.DeepClone());
            }
            sources.Capture(callId, OperationTool, arguments, result);
            references.Add(new JObject { ["toolCallId"] = callId, ["path"] = "/items" });
            if (!more) break;
            if (page == 23) return Finish("Nc.OperationIncomplete");
            offset = next;
        }
        if (rows.Count == 0 || document == null) return Finish("Nc.NoOperations");

        var operationQuestion = sources.Create(new JObject
        {
            ["question"] = StudioStrings.Get("Nc.SelectOperations"),
            ["kind"] = "select", ["itemKind"] = "operation", ["multiple"] = true, ["sources"] = references
        });
        var operationQuestionId = "nc-operation-question-" + Guid.NewGuid().ToString("N");
        var operationInput = new JObject { ["question"] = operationQuestion.Title, ["kind"] = "select", ["itemKind"] = "operation", ["multiple"] = true };
        turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = operationQuestionId, Name = QuestionSources.ToolName, Arguments = operationInput, ClientInitiated = true }] });
        trace(new ChatTrace("Tool call", QuestionSources.ToolName + " " + operationInput.ToString(Formatting.None), QuestionSources.ToolName, operationInput));
        var operationAnswer = await ask(operationQuestion, token);
        token.ThrowIfCancellationRequested();
        var operationReceipt = new McpToolResult { StructuredContent = operationAnswer?.Data ?? new JObject { ["status"] = "cancelled" } };
        turn.Add(new AiMessage { Role = "tool", ToolCallId = operationQuestionId, ToolName = QuestionSources.ToolName, Content = ToolResultContext.Serialize(operationReceipt) });
        trace(new ChatTrace("Tool result", ToolResultContext.Serialize(operationReceipt), QuestionSources.ToolName, operationInput));
        if (operationAnswer == null) return Finish("Nc.Cancelled");

        var selectedOperations = SelectedOperations(operationAnswer, document);
        if (selectedOperations.Count == 0) return Finish("Nc.NoOperations");

        var ppArguments = new JObject { ["documentId"] = document };
        var ppCallId = "nc-postprocessor-" + Guid.NewGuid().ToString("N");
        var ppResult = await ReadTool(mcp, PostProcessorTool, ppArguments, ppCallId, turn, trace, token);
        var ppData = PdmInventory.Data(ppResult);
        var configuredPostProcessor = (string?)ppData?["value"];
        if (ppResult.IsError || string.IsNullOrWhiteSpace(configuredPostProcessor)) return Finish("Nc.PostProcessorUnavailable");

        // The 7.20 INCPostProcessor contract exposes the configured PP id, but no PP catalog/list method.
        // Keep the selection step explicit and receipt-backed instead of inventing unavailable PP names.
        var ppSources = new QuestionSources();
        var ppChoice = new JObject { ["name"] = configuredPostProcessor, ["postProcessorId"] = configuredPostProcessor, ["documentId"] = document };
        ppSources.Capture("nc-postprocessor-choice", PostProcessorTool, ppArguments,
            new McpToolResult { StructuredContent = new JObject { ["items"] = new JArray(ppChoice) } });
        var ppQuestion = ppSources.Create(new JObject
        {
            ["question"] = StudioStrings.Get("Nc.SelectPostProcessor"), ["kind"] = "select", ["itemKind"] = "option",
            ["multiple"] = false, ["sources"] = new JArray(new JObject { ["toolCallId"] = "nc-postprocessor-choice", ["path"] = "/items" })
        });
        var ppQuestionId = "nc-postprocessor-question-" + Guid.NewGuid().ToString("N");
        var ppInput = new JObject { ["question"] = ppQuestion.Title, ["kind"] = "select", ["itemKind"] = "option" };
        turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = ppQuestionId, Name = QuestionSources.ToolName, Arguments = ppInput, ClientInitiated = true }] });
        trace(new ChatTrace("Tool call", QuestionSources.ToolName + " " + ppInput.ToString(Formatting.None), QuestionSources.ToolName, ppInput));
        var ppAnswer = await ask(ppQuestion, token);
        token.ThrowIfCancellationRequested();
        var ppReceipt = new McpToolResult { StructuredContent = ppAnswer?.Data ?? new JObject { ["status"] = "cancelled" } };
        turn.Add(new AiMessage { Role = "tool", ToolCallId = ppQuestionId, ToolName = QuestionSources.ToolName, Content = ToolResultContext.Serialize(ppReceipt) });
        trace(new ChatTrace("Tool result", ToolResultContext.Serialize(ppReceipt), QuestionSources.ToolName, ppInput));
        if (ppAnswer == null) return Finish("Nc.Cancelled");
        var selectedPostProcessor = (string?)ppAnswer.Data["selected"]?[0]?["value"]?["postProcessorId"];
        if (string.IsNullOrWhiteSpace(selectedPostProcessor)) return Finish("Nc.PostProcessorUnavailable");

        var generateArguments = new JObject
        {
            ["documentId"] = document,
            ["operations"] = new JArray(selectedOperations),
            ["postProcessorId"] = selectedPostProcessor
        };
        var generationCall = await CallConfirmed(mcp, GenerateTool, generateArguments, confirm, turn, trace, token);
        if (generationCall.Declined) return Finish("Nc.Cancelled");
        var generated = generationCall.Result;
        var generatedData = PdmInventory.Data(generated);
        if (generated.IsError || generatedData?["ncFiles"] is not JArray ncFiles || ncFiles.Count == 0)
            return Finish("Nc.GenerationFailed");

        var saved = 0;
        foreach (var file in ncFiles.OfType<JObject>())
        {
            token.ThrowIfCancellationRequested();
            var destination = await chooseDestination(file, token);
            if (string.IsNullOrWhiteSpace(destination))
                return Finish("Nc.SaveCancelled", saved, ncFiles.Count);
            if (file["element"] is not JObject element || string.IsNullOrWhiteSpace((string?)element["documentId"]))
                return Finish("Nc.SaveFailed", "NC entity identity is missing.");
            var exportArguments = new JObject
            {
                ["documentId"] = element["documentId"],
                ["ncFile"] = element.DeepClone(),
                ["fileName"] = destination
            };
            var exportCall = await CallConfirmed(mcp, ExportTool, exportArguments, confirm, turn, trace, token);
            if (exportCall.Declined) return Finish("Nc.SaveCancelled", saved, ncFiles.Count);
            var exported = exportCall.Result;
            if (exported.IsError || (bool?)PdmInventory.Data(exported)?["exported"] != true)
                return Finish("Nc.SaveFailed", (string?)PdmInventory.Data(exported)?["detail"] ?? "TopSolid could not export the NC file.");
            saved++;
        }
        return Finish("Nc.Completed", saved);
    }

    private static bool HasTool(IMcpClient mcp, string name, bool confirmation) =>
        mcp.Tools.Any(tool => tool.Name == name && tool.RequiresConfirmation == confirmation);

    private static async Task<McpToolResult> ReadTool(IMcpClient mcp, string name, JObject arguments, string callId,
        List<AiMessage> turn, Action<ChatTrace> trace, CancellationToken token)
    {
        turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = callId, Name = name, Arguments = arguments, ClientInitiated = true }] });
        trace(new ChatTrace("Tool call", name + " " + arguments.ToString(Formatting.None), name, arguments));
        var result = await mcp.CallToolAsync(name, arguments, token);
        var content = ToolResultContext.Serialize(result);
        turn.Add(new AiMessage { Role = "tool", ToolCallId = callId, ToolName = name, Content = content });
        trace(new ChatTrace(result.IsError ? "Tool error" : "Tool result", content, name, arguments));
        return result;
    }

    private static async Task<(McpToolResult Result, bool Declined)> CallConfirmed(IMcpClient mcp, string name, JObject arguments,
        Func<JObject, CancellationToken, Task<bool>> confirm, List<AiMessage> turn, Action<ChatTrace> trace,
        CancellationToken token)
    {
        var callId = "nc-action-" + Guid.NewGuid().ToString("N");
        turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = callId, Name = name, Arguments = arguments, ClientInitiated = true }] });
        trace(new ChatTrace("Tool call", name + " " + arguments.ToString(Formatting.None), name, arguments));
        if (mcp is not IConfirmableMcpClient confirmable)
        {
            var unavailable = McpToolResult.Error("This client cannot confirm the NC action. No NC output was generated.");
            turn.Add(new AiMessage { Role = "tool", ToolCallId = callId, ToolName = name, Content = ToolResultContext.Serialize(unavailable) });
            trace(new ChatTrace("Tool error", ToolResultContext.Serialize(unavailable), name, arguments));
            return (unavailable, false);
        }
        JObject proposal;
        try { proposal = await confirmable.PrepareToolAsync(name, arguments, token); }
        catch (Exception error)
        {
            var failure = McpToolResult.Error("The NC action was not prepared: " + error.Message);
            turn.Add(new AiMessage { Role = "tool", ToolCallId = callId, ToolName = name, Content = ToolResultContext.Serialize(failure) });
            trace(new ChatTrace("Tool error", ToolResultContext.Serialize(failure), name, arguments));
            return (failure, false);
        }
        if ((string?)proposal["toolName"] != name || proposal["arguments"] is not JObject || proposal["target"] is not JObject || string.IsNullOrWhiteSpace((string?)proposal["confirmationToken"]))
            throw new InvalidOperationException("TopSolid returned an invalid NC confirmation proposal.");
        var visible = (JObject)proposal.DeepClone(); visible.Remove("confirmationToken");
        if (!await confirm(visible, token))
        {
            var rejected = McpToolResult.Error("The user declined the NC action. No further NC action was executed.");
            turn.Add(new AiMessage { Role = "tool", ToolCallId = callId, ToolName = name, Content = ToolResultContext.Serialize(rejected) });
            trace(new ChatTrace("Tool error", ToolResultContext.Serialize(rejected), name, arguments));
            return (rejected, true);
        }
        var result = await confirmable.CallConfirmedToolAsync(name, arguments, (string)proposal["confirmationToken"]!, token);
        var content = ToolResultContext.Serialize(result);
        turn.Add(new AiMessage { Role = "tool", ToolCallId = callId, ToolName = name, Content = content });
        trace(new ChatTrace(result.IsError ? "Tool error" : "CAD change", content, name, arguments));
        return (result, false);
    }

    private static List<JObject> SelectedOperations(QuestionAnswer answer, string document)
    {
        var selected = new List<JObject>();
        if (answer.Data["selected"] is not JArray rows) return selected;
        foreach (var item in rows.OfType<JObject>())
        {
            if (item["value"] is not JObject row || row["operation"] is not JObject operation) continue;
            var reference = (JObject)operation.DeepClone();
            if (reference["element"] is JObject element && (string?)element["documentId"] != document) continue;
            selected.Add(reference);
        }
        return selected;
    }
}
