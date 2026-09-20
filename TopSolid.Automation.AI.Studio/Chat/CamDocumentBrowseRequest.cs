using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

// Interpret only the current typed request. Each picker owns one scope and its
// receipt-backed values; the model never chooses a document for this workflow.
internal static class CamDocumentBrowseRequest
{
    private const string Projects = "topsolid_list_projects", Children = "topsolid_list_pdm_children";
    private const string Open = "topsolid_open_document";
    private static bool Has(string text, string pattern) => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static bool Operations(string text) => Has(text, @"오퍼레이션|가공\s*작업|\boperations?\b");
    internal static bool Matches(string text)
    {
        if (Has(text, @"수정|변경|삭제|생성|저장|파라미터|매개변수|미리\s*보기|툴패스|\b(?:edit|change|modify|delete|create|save|export|generate|simulate|verify|parameter|preview|toolpath|explain|compare)\b|설명|비교")) return false;
        if (!Has(text, @"목록|리스트|보여|볼래|선택|고르|\b(?:list|show|display|select|choose|pick)\b")) return false;
        var documents = Has(text, @"문서|도큐먼트|\b(?:documents?|docs?|files?)\b");
        var project = Has(text, @"프로젝트|\bprojects?\b");
        if (project && documents) return true;
        if (!Operations(text)) return false;
        // Explicit active-document commands retain their existing direct path.
        if (!project && Has(text, @"현재|활성|\b(?:current|active)\b")) return false;
        return documents && Has(text, @"선택|고르|\b(?:select|choose|pick)\b") ||
            ListPresentation.DirectTools(Regex.Replace(text, @"\b(?:just|only)\b|그냥", "", RegexOptions.IgnoreCase)).Contains(CamSelectionRequest.Tool);
    }

    internal static async Task<IReadOnlyList<AiMessage>?> Run(string text, IMcpClient mcp,
        Func<UserQuestion, CancellationToken, Task<QuestionAnswer?>>? ask,
        Func<UserQuestion, CancellationToken, Task>? show,
        Func<JObject, CancellationToken, Task<bool>>? confirm, Action<ChatTrace> trace, CancellationToken token)
    {
        if (!Matches(text)) return null;
        var turn = new List<AiMessage> { new() { Role = "user", Content = text } };
        IReadOnlyList<AiMessage> Finish(string key, params object[] args)
        { turn.Add(new AiMessage { Role = "assistant", Content = StudioStrings.Get(key, args) }); return turn; }
        var operations = Operations(text);
        var camOnly = operations || Has(text, @"가공|\b(?:CAM|machining)\b");
        if (!mcp.IsConnected || ask == null || !Available(Projects) || !Available(Children) ||
            operations && (show == null || !Available(CamSelectionRequest.Tool))) return Finish("CamBrowse.Unavailable");

        try
        {
            var projectBrowser = new Pages(Projects, new JObject(), "project", StudioStrings.Get("CamBrowse.Project"), "", false, _ => true, Call);
            var navigation = new List<(JObject Row, Pages Browser)>();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var browser = navigation.Count == 0 ? projectBrowser : navigation[^1].Browser;
                var question = await browser.Snapshot(false, token);
                if (navigation.Count == 0 && question.Choices.Count == 0) return Finish("Question.EmptySelection");
                var answer = await ask(question, token);
                token.ThrowIfCancellationRequested();
                Record(QuestionSources.ToolName, new JObject { ["question"] = question.Title, ["kind"] = "select", ["itemKind"] = question.ItemKind },
                    new McpToolResult { StructuredContent = answer?.Data ?? new JObject { ["status"] = "cancelled" } });
                if (answer == null) return Finish("CamBrowse.Cancelled");
                if ((string?)answer.Data["status"] == "back")
                { if (navigation.Count > 0) navigation.RemoveAt(navigation.Count - 1); continue; }
                // Reject injected or stale answers, including an answer from a different step.
                var row = browser.Selected(answer);
                if (row == null) return Finish("CamBrowse.Unavailable");
                if (navigation.Count == 0 || (string?)row["kind"] == "folder")
                {
                    if (navigation.Count >= 64 || navigation.Any(n => (string?)n.Row["pdmObjectId"] == (string?)row["pdmObjectId"]))
                        return Finish("List.Incomplete");
                    var path = string.Join(" → ", navigation.Select(n => (string?)n.Row["name"]).Append((string?)row["name"]));
                    navigation.Add((row, new Pages(Children, new JObject { ["pdmObjectId"] = row["pdmObjectId"]!.DeepClone() }, "document",
                        StudioStrings.Get(camOnly ? "CamBrowse.Document" : "CamBrowse.Documents"), path, true,
                        r => (string?)r["kind"] == "folder" || (string?)r["kind"] == "document" && (!camOnly || IsCam(r)), Call)));
                    continue;
                }
                if (!operations) return Finish("Selection.Selected", answer.Summary);
                var document = (string?)row["documentId"];
                if (string.IsNullOrWhiteSpace(document)) return Finish("CamBrowse.Unavailable");
                if ((bool?)row["isLoaded"] != true)
                {
                    if (mcp is not IConfirmableMcpClient client || confirm == null || !mcp.Tools.Any(t => t.Name == Open && t.RequiresConfirmation))
                        return Finish("CamBrowse.Unavailable");
                    var args = new JObject { ["documentId"] = document };
                    var proposal = await client.PrepareToolAsync(Open, args, token);
                    if ((string?)proposal["toolName"] != Open || !JToken.DeepEquals(proposal["arguments"], args) ||
                        proposal["target"] is not JObject || string.IsNullOrWhiteSpace((string?)proposal["confirmationToken"]))
                        return Finish("CamBrowse.Unavailable");
                    var visible = (JObject)proposal.DeepClone(); visible.Remove("confirmationToken");
                    if (!await confirm(visible, token)) return Finish("CamBrowse.Cancelled");
                    token.ThrowIfCancellationRequested();
                    var opened = await client.CallConfirmedToolAsync(Open, args, (string)proposal["confirmationToken"]!, token);
                    Record(Open, args, opened);
                    var data = PdmInventory.Data(opened);
                    if (opened.IsError || (bool?)data?["opened"] != true || (string?)data["originalDocumentId"] != document ||
                        string.IsNullOrWhiteSpace((string?)data["documentId"])) return Finish("CamBrowse.Unavailable");
                    document = (string)data["documentId"]!;
                }
                var context = string.Join(" → ", navigation.Select(n => (string?)n.Row["name"]).Append((string?)row["name"]));
                var operationBrowser = new Pages(CamSelectionRequest.Tool, new JObject { ["documentId"] = document }, "operation",
                    StudioStrings.Get("Cam.Operations"), context, false, _ => true, Call);
                var operationQuestion = await operationBrowser.Snapshot(true, token);
                if (operationQuestion.Choices.Count == 0) return Finish("Cam.NoMatch");
                await show!(operationQuestion, token);
                token.ThrowIfCancellationRequested();
                return Finish("CamBrowse.Shown", (string?)row["name"] ?? answer.Summary);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or JsonException or System.IO.IOException)
        {
            trace(new ChatTrace("Tool error", error.Message));
            return Finish("CamBrowse.Unavailable");
        }

        bool Available(string name) => mcp.Tools.Any(t => t.Name == name && !t.RequiresConfirmation);
        void Record(string name, JObject args, McpToolResult result)
        {
            var id = "document-browse-" + Guid.NewGuid().ToString("N");
            turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = id, Name = name, Arguments = (JObject)args.DeepClone(), ClientInitiated = true }] });
            var content = ToolResultContext.Serialize(result);
            turn.Add(new AiMessage { Role = "tool", ToolCallId = id, ToolName = name, Content = content });
            trace(new ChatTrace(result.IsError ? "Tool error" : "Tool result", content, name, args));
        }
        async Task<McpToolResult> Call(string name, JObject args, CancellationToken ct)
        {
            if (!Available(name)) throw new InvalidOperationException("The selected list tool is unavailable.");
            trace(new ChatTrace("Tool call", name + " " + args.ToString(Formatting.None), name, args));
            var result = await mcp.CallToolAsync(name, args, ct);
            Record(name, args, result);
            return result;
        }
    }

    private static bool IsCam(JObject row) => string.Equals((string?)row["extension"], ".TopMillTurn", StringComparison.OrdinalIgnoreCase) ||
        ((string?)row["typeFullName"] ?? (string?)row["type"] ?? "").StartsWith("TopSolid.Cam.", StringComparison.Ordinal);

    // Keep raw pagination independent of filtered visible rows. An empty filtered
    // page must not hide CAM documents on later pages or lose its native offsets.
    private sealed class Pages(string tool, JObject scope, string kind, string title, string context, bool back,
        Func<JObject, bool> include, Func<string, JObject, CancellationToken, Task<McpToolResult>> call)
    {
        private readonly List<UserQuestion> pages = [];
        private readonly List<JObject> receipts = [];
        private readonly HashSet<string> identities = new(StringComparer.Ordinal);
        private int offset, pageCount;
        private int? total;
        private bool more = true;

        internal JObject? Selected(QuestionAnswer answer)
        {
            if (answer.Data["selected"] is not JArray { Count: 1 } selected || selected[0] is not JObject receipt ||
                !receipts.Any(r => JToken.DeepEquals(r, receipt))) return null;
            return receipt["value"] as JObject;
        }

        internal async Task<UserQuestion> Snapshot(bool browse, CancellationToken token)
        {
            if (pageCount == 0) await Fetch(token);
            return UserQuestion.Browse(pages, kind == "operation" ? total ?? pages.Count : pages.Count, more,
                more ? async ct => { await Fetch(ct); return await Snapshot(browse, ct); } : null,
                browse, title, itemKindOverride: kind, context: pages.Count == 0 && back ? context + "\n" + StudioStrings.Get("CamBrowse.Empty") : context,
                canGoBack: back);
        }

        private async Task Fetch(CancellationToken token)
        {
            var visibleBefore = pages.Count;
            do
            {
                token.ThrowIfCancellationRequested();
                if (++pageCount > 250) throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
                var args = (JObject)scope.DeepClone(); args["offset"] = offset; args["limit"] = 20;
                var result = await call(tool, args, token);
                var data = PdmInventory.Data(result);
                if (result.IsError || ToolResultContext.Serialize(result).Length > 64000 || data?["items"] is not JArray items ||
                    data["total"]?.Type != JTokenType.Integer || data["offset"]?.Type != JTokenType.Integer || (int?)data["offset"] != offset ||
                    data["hasMore"]?.Type != JTokenType.Boolean || (int?)data["failed"] > 0 || items.Count > 20)
                    throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
                var count = (int)data["total"]!;
                var next = offset + items.Count;
                more = (bool)data["hasMore"]!;
                if (count < next || total.HasValue && count != total || more != (next < count) ||
                    more && (next <= offset || data["nextOffset"] != null && data["nextOffset"]!.Type != JTokenType.Null && (int?)data["nextOffset"] != next))
                    throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
                total = count;
                var sources = new QuestionSources(); sources.Capture("page", tool, args, result);
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i] is not JObject row || (bool?)row["isError"] == true) throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
                    var identity = kind == "operation" ? row["operation"] as JObject : null;
                    var handle = identity?["element"] as JObject ?? identity;
                    var key = kind == "operation" ? identity?.ToString(Formatting.None) : (string?)row["pdmObjectId"];
                    if (string.IsNullOrWhiteSpace(key) || !identities.Add(key) || kind == "operation" &&
                        (!JToken.DeepEquals(handle?["documentId"], scope["documentId"]) || handle?["id"] == null))
                        throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
                    if (!include(row)) continue;
                    var question = sources.Create(new JObject { ["question"] = title, ["kind"] = "select", ["itemKind"] = kind,
                        ["sources"] = new JArray(new JObject { ["toolCallId"] = "page", ["path"] = "/items/" + i }) });
                    pages.Add(question);
                    receipts.Add((JObject)question.Answer(selectedKeys: [question.Choices[0].Key]).Data["selected"]![0]!.DeepClone());
                }
                offset = next;
            } while (more && pages.Count == visibleBefore);
        }
    }
}
