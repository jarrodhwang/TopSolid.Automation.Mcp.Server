using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

/// <summary>Read-only list browsing is a client action, independent of whether a model asks for a dialog.</summary>
internal sealed class ListPresentation
{
    private sealed record Page(string Tool, JObject Arguments, JObject Data);
    private readonly List<Page> pages = [];
    private bool incomplete;
    internal bool HasItems => pages.Any(p => p.Data["items"] is JArray { Count: > 0 });
    internal bool IsEmpty => pages.Count > 0 && !HasItems && !incomplete;
    internal static bool Requested(string text) => Regex.IsMatch(text,
        @"목록|리스트|\blist\b|\benumerate\b|(?:파트|도큐먼트|문서|파라미터|매개변수|엔터티|엔티티|프로젝트|공구|라이브러리|오퍼레이션)(?:들|\s)*(?:을|를)?\s*(?:보여|알려)|(?:보여|알려).*(?:프로젝트|라이브러리)|\bshow\b.*\b(?:parts|documents|parameters|entities|projects|tools|libraries|operations)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static string Kind(string tool) => tool switch
    {
        "topsolid_list_projects" or "topsolid_search_projects" => "project",
        "topsolid_list_libraries" => "library",
        "topsolid_list_sketches2d" or "topsolid_list_sketches3d" => "sketch",
        "topsolid_list_shape_summaries" => "shape",
        _ when tool.Contains("cam_operation") || tool.EndsWith("cam_scenario") => "operation",
        _ when tool.Contains("cam_parameter") => "camParameter",
        _ when tool.Contains("parameter") => "parameter",
        _ when tool.Contains("document") || tool.Contains("revision") => "document",
        _ when tool.Contains("tool") => "tool",
        _ when tool.Contains("part") || tool.Contains("occurrence") => "part",
        _ when tool.Contains("shape") => "shape",
        _ => "element"
    };

    internal void Capture(string tool, JObject args, McpToolResult result)
    {
        if (result.IsError || !(tool.StartsWith("topsolid_list_", StringComparison.Ordinal) || tool is "topsolid_search_documents" or "topsolid_search_projects") ||
            tool == "topsolid_list_active_licenses" || PdmInventory.Data(result) is not { } data || data["items"] is not JArray items) return;
        // Never reinterpret raw handles or prose as named objects. A typed list tool must return object rows.
        if (items.Any(r => r is not JObject)) return;
        var offset = (int?)data["offset"] ?? 0;
        if (offset < 0 || items.Count > 500) { incomplete = true; return; }
        if (args["documentId"] is JValue document && items.OfType<JObject>().Any(row =>
            (row["documentId"] ?? row["element"]?["documentId"] ?? row["operation"]?["element"]?["documentId"] ?? row["parameter"]?["documentId"]) is { } returned && !JToken.DeepEquals(returned, document)))
        { incomplete = true; return; }
        if (data["total"]?.Type == JTokenType.Integer && ((int)data["total"]! < offset + items.Count ||
            (bool?)data["hasMore"] != (offset + items.Count < (int)data["total"]!))) { incomplete = true; return; }
        var scope = Scope(args);
        var existing = pages.Where(p => p.Tool == tool && JToken.DeepEquals(Scope(p.Arguments), scope)).ToArray();
        if (existing.Any(p => (int?)p.Data["offset"] == offset)) return;
        if (offset != existing.Sum(p => ((JArray)p.Data["items"]!).Count)) { incomplete = true; return; }
        if (data["total"]?.Type == JTokenType.Integer && existing.Any(p => (int?)p.Data["total"] != (int?)data["total"]))
        { incomplete = true; return; }
        pages.Add(new Page(tool, (JObject)args.DeepClone(), (JObject)data.DeepClone()));
    }

    private static JObject Scope(JObject args)
    { var scope = (JObject)args.DeepClone(); scope.Remove("offset"); scope.Remove("limit"); return scope; }

    private UserQuestion Snapshot(bool browse, string title, bool multiple,
        Func<CancellationToken, Task<UserQuestion>>? loadMore)
    {
        var questions = new List<UserQuestion>();
        var total = 0; var more = false;
        foreach (var group in pages.GroupBy(p => p.Tool + Scope(p.Arguments).ToString(Formatting.None)))
        {
            total += (int?)group.First().Data["total"] ?? group.Sum(p => ((JArray)p.Data["items"]!).Count);
            more |= (bool?)group.Last().Data["hasMore"] == true;
            foreach (var page in group)
            {
                // Split large pages for the same bounded receipt validation used by selection dialogs.
                var rows = (JArray)page.Data["items"]!;
                foreach (var chunk in rows.Chunk(20))
                {
                    var sources = new QuestionSources();
                    sources.Capture("list-page", page.Tool, page.Arguments,
                        new McpToolResult { StructuredContent = new JObject { ["items"] = new JArray(chunk.Select(r => r.DeepClone())) } });
                    questions.Add(sources.Create(new JObject { ["question"] = title, ["kind"] = "select",
                        ["itemKind"] = Kind(page.Tool), ["sources"] = new JArray(new JObject { ["toolCallId"] = "list-page", ["path"] = "/items" }) }, browse: true));
                }
            }
        }
        return UserQuestion.Browse(questions, total, more || incomplete,
            more && !incomplete && pages.Count < 250 ? loadMore : null,
            browse, title, multiple);
    }

    private async Task FetchNextPage(IMcpClient mcp, Action<ChatTrace> trace, CancellationToken ct)
    {
        var pending = pages.GroupBy(p => p.Tool + Scope(p.Arguments).ToString(Formatting.None)).Select(g => g.Last())
            .FirstOrDefault(p => (bool?)p.Data["hasMore"] == true);
        if (pending == null) return;
        var args = (JObject)pending.Arguments.DeepClone();
        var offset = (int?)pending.Data["offset"] ?? 0;
        var next = (int?)pending.Data["nextOffset"] ?? offset + ((JArray)pending.Data["items"]!).Count;
        if (next <= offset || !mcp.Tools.Any(t => t.Name == pending.Tool && !t.RequiresConfirmation))
            throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
        // Pin the document from actual returned identities before fetching another page.
        if (args["documentId"] == null && mcp.Tools.Single(t => t.Name == pending.Tool).InputSchema["properties"]?["documentId"] != null)
        {
            var ids = pending.Data.Descendants().OfType<JProperty>().Where(p => p.Name == "documentId" && p.Value.Type == JTokenType.String)
                .Select(p => (string)p.Value!).Distinct().ToArray();
            if (ids.Length != 1) throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
            // Update every page's scope together, retaining stable grouping and identity validation.
            foreach (var page in pages.Where(p => p.Tool == pending.Tool && JToken.DeepEquals(Scope(p.Arguments), Scope(pending.Arguments))).ToArray())
                page.Arguments["documentId"] = ids[0];
            args["documentId"] = ids[0];
        }
        args["offset"] = next; args["limit"] = 20;
        trace(new ChatTrace("Tool call", pending.Tool + " " + args.ToString(Formatting.None)));
        var receipt = await mcp.CallToolAsync(pending.Tool, args, ct);
        trace(new ChatTrace(receipt.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(receipt), pending.Tool, args));
        if (receipt.IsError) throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
        var before = pages.Count; Capture(pending.Tool, args, receipt);
        if (pages.Count == before) throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
    }

    internal async Task<bool> ShowAsync(IMcpClient mcp, Func<UserQuestion, CancellationToken, Task>? show,
        Action<ChatTrace> trace, CancellationToken token)
    {
        // Do not open the same blank picker that appeared in the attached
        // conversation log. A successful zero-row result is useful chat
        // information, but not a useful modal interaction.
        if (show == null || pages.Count == 0 || !HasItems) return false;
        async Task<UserQuestion> LoadMore(CancellationToken ct)
        {
            await FetchNextPage(mcp, trace, ct);
            return Snapshot(true, StudioStrings.Get("List.Title"), false, LoadMore);
        }
        await show(Snapshot(true, StudioStrings.Get("List.Title"), false, LoadMore), token);
        token.ThrowIfCancellationRequested();
        return true;
    }

    internal async Task<QuestionAnswer?> AskSelectionAsync(IMcpClient mcp,
        Func<UserQuestion, CancellationToken, Task<QuestionAnswer?>>? ask, string title, bool multiple,
        Action<ChatTrace> trace, CancellationToken token)
    {
        if (ask == null || pages.Count == 0 || !HasItems) return null;
        async Task<UserQuestion> LoadMore(CancellationToken ct)
        {
            await FetchNextPage(mcp, trace, ct);
            return Snapshot(false, title, multiple, LoadMore);
        }
        return await ask(Snapshot(false, title, multiple, LoadMore), token);
    }

    // Only simple, unqualified browse commands use this shortcut. Named/filtered scopes go through the normal tool loop.
    internal static string[] DirectTools(string text)
    {
        if (!Requested(text) || Regex.IsMatch(text, @"선택|수정|변경|삭제|생성|저장|설명|비교|\b(?:select|change|edit|delete|create|save|explain|compare|where|named|called)\b|[\d\""']", RegexOptions.IgnoreCase)) return [];
        if (PdmListRequest.Parse(text) != null) return []; // Existing PDM resolver preserves requested ordering/dates.
        var simple = Regex.Replace(text, @"현재|활성|열려\s*있는|열린|가공|의|을|를|들|전체|모든|좀|목록|리스트|보여\s*줘|보여\s*주세요|알려\s*줘|\b(?:please|show|list|all|the|current|active|open|of|me|machining|cam)\b|[\s.?!,]", "", RegexOptions.IgnoreCase);
        return simple.ToLowerInvariant() switch
        {
            "오퍼레이션" or "도큐먼트오퍼레이션" or "문서오퍼레이션" or "작업" or "operations" or "operation" => [CamSelectionRequest.Tool],
            "도큐먼트" or "문서" or "documents" or "document" => ["topsolid_list_document_summaries"],
            "파라미터" or "매개변수" or "parameters" => ["topsolid_list_parameter_values"],
            "엔터티" or "엔티티" or "요소" or "entities" or "elements" => ["topsolid_list_named_elements"],
            "공구" or "문서공구" or "도큐먼트공구" or "tools" => ["topsolid_list_cam_tools"],
            "프로젝트" or "projects" => ["topsolid_list_projects"],
            "라이브러리" or "libraries" => ["topsolid_list_libraries"],
            _ => []
        };
    }

    internal static async Task<IReadOnlyList<AiMessage>?> RunDirect(string text, IMcpClient mcp,
        Func<UserQuestion, CancellationToken, Task>? show, Action<ChatTrace> trace, CancellationToken token)
    {
        var names = DirectTools(text);
        if (show == null || names.Length == 0 || !mcp.IsConnected || names.Any(n => !mcp.Tools.Any(t => t.Name == n && !t.RequiresConfirmation))) return null;
        var browser = new ListPresentation(); var turn = new List<AiMessage> { new() { Role = "user", Content = text } };
        foreach (var name in names)
        {
            var args = new JObject { ["offset"] = 0, ["limit"] = 20 }; var id = "browse-" + Guid.NewGuid().ToString("N");
            turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = id, Name = name, Arguments = args, ClientInitiated = true }] });
            trace(new ChatTrace("Tool call", name + " " + args.ToString(Formatting.None)));
            var result = await mcp.CallToolAsync(name, args, token);
            trace(new ChatTrace(result.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(result), name, args));
            turn.Add(new AiMessage { Role = "tool", ToolCallId = id, ToolName = name, Content = ToolResultContext.Serialize(result) });
            browser.Capture(name, args, result);
        }
        var shown = await browser.ShowAsync(mcp, show, trace, token);
        var message = browser.IsEmpty ? "List.Empty" : shown ? "List.Shown" : "List.Unavailable";
        turn.Add(new AiMessage { Role = "assistant", Content = StudioStrings.Get(message) });
        return turn;
    }
}
