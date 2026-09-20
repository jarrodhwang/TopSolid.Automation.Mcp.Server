using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.AI.Studio.Appearance;

namespace TopSolid.Automation.AI.Studio.Chat;

public sealed record QuestionChoice(string Key, string Label, string Detail, string Kind, string SearchText, string? IconKey = null,
    string? ToolText = null, string? ToolIconKey = null, string? ToolGroupKey = null);

public sealed class QuestionAnswer
{
    internal QuestionAnswer(JObject data, string summary, ChatAttachment? image = null)
    { Data = data; Summary = summary; Image = image; }
    internal JObject Data { get; }
    public string Summary { get; }
    public ChatAttachment? Image { get; }
}

/// <summary>Display choices and their immutable, receipt-backed values are kept separate.</summary>
public sealed class UserQuestion
{
    private readonly Dictionary<string, JObject> values;
    internal UserQuestion(string title, string kind, string itemKind, bool multiple, string unit,
        decimal? minimum, decimal? maximum, IReadOnlyList<QuestionChoice> choices, Dictionary<string, JObject> values,
        string? context = null)
    {
        Title = title; Kind = kind; ItemKind = itemKind; Multiple = multiple; Unit = unit; Minimum = minimum; Maximum = maximum;
        Choices = choices; this.values = values; Context = context ?? ContextFor(kind, itemKind, choices, values);
    }
    public string Title { get; }
    public string Kind { get; }
    public string ItemKind { get; }
    public bool Multiple { get; }
    public string Unit { get; }
    public decimal? Minimum { get; }
    public decimal? Maximum { get; }
    public IReadOnlyList<QuestionChoice> Choices { get; }
    /// <summary>Client-derived scope guidance for the next selection step.</summary>
    public string Context { get; internal init; }
    public bool CanGoBack { get; internal init; }
    internal bool HasNavigationContext { get; init; }
    internal static QuestionAnswer Back() => new(new JObject { ["status"] = "back" }, "");
    public bool IsBrowse { get; internal init; }
    // Only for an explicitly resolved operation preview, never a model-selected default.
    internal string? InitialInspectionKey { get; set; }
    public bool HasMore { get; internal init; }
    public int Total { get; internal init; }
    internal Func<CancellationToken, Task<UserQuestion>>? LoadMoreAsync { get; init; }
    private JObject? documentPreview;
    internal JObject? DocumentPreview
    {
        get
        {
            if (documentPreview != null) return (JObject)documentPreview.DeepClone();
            if (Choices.Count == 0 || Choices.Any(c => c.Kind != "operation")) return null;
            var ids = Choices.Select(c => (string?)PreviewTargetFor(c.Key)?["documentId"]).Distinct().ToArray();
            return ids is [not null] ? new JObject { ["documentId"] = ids[0] } : null;
        }
        init => documentPreview = value;
    }
    internal static UserQuestion Browse(IEnumerable<UserQuestion> pages, int total, bool more,
        Func<CancellationToken, Task<UserQuestion>>? loadMore, bool browse = true, string? title = null,
        bool multiple = false, string? itemKindOverride = null, string? context = null, bool canGoBack = false)
    {
        var choices = new List<QuestionChoice>(); var values = new Dictionary<string, JObject>();
        foreach (var page in pages)
            foreach (var choice in page.Choices)
            {
                var key = "browse-" + choices.Count;
                choices.Add(choice with { Key = key }); values.Add(key, (JObject)page.values[choice.Key].DeepClone());
            }
        var kind = choices.Select(c => c.Kind).Distinct().Take(2).ToArray();
        var targets = values.Values.Select(v => Preview.PreviewTarget.FromChoice(v, "element"))
            .Where(v => v != null).Select(v => (string?)v!["documentId"]).Distinct().ToArray();
        return new UserQuestion(title ?? StudioStrings.Get("List.Title"), "select", itemKindOverride ?? (kind.Length == 1 ? kind[0] : "option"), multiple, "", null, null, choices, values)
        { IsBrowse = browse, Total = total, HasMore = more, LoadMoreAsync = loadMore,
            Context = context ?? ContextFor("select", itemKindOverride ?? (kind.Length == 1 ? kind[0] : "option"), choices, values), CanGoBack = canGoBack, HasNavigationContext = context != null,
            DocumentPreview = kind is ["operation"] && targets is [not null] ? new JObject { ["documentId"] = targets[0] } : null };
    }
    internal JObject? PreviewTargetFor(string? key) => key != null && values.TryGetValue(key, out var receipt)
        ? Preview.PreviewTarget.FromChoice(receipt, Choices.First(c => c.Key == key).Kind) : null;

    // A native edit can replace the document revision. Rebase identities only from
    // the successful server receipt so subsequent selections still target this operation.
    internal void RebaseDocument(string before, string after)
    {
        if (before == after) return;
        foreach (var receipt in values.Values.Append(documentPreview).OfType<JObject>())
            foreach (var property in receipt.Descendants().OfType<JProperty>().Where(p => p.Name == "documentId" && (string?)p.Value == before).ToArray())
                property.Value = after;
    }

    private static string ContextFor(string kind, string itemKind, IReadOnlyList<QuestionChoice> choices,
        IReadOnlyDictionary<string, JObject> values)
    {
        if (kind != "select") return "";
        var effectiveKind = itemKind;
        if (effectiveKind == "option")
        {
            var rowKinds = choices.Select(c => c.Kind).Distinct(StringComparer.Ordinal).ToArray();
            if (rowKinds.Length == 1) effectiveKind = rowKinds[0];
        }
        return effectiveKind switch
        {
            "project" => StudioStrings.Get("Question.ProjectContext"),
            "library" => StudioStrings.Get("Question.LibraryContext"),
            "operation" => StudioStrings.Get("Question.OperationContext"),
            "document" => DocumentContext(values),
            _ => ""
        };
    }

    private static string DocumentContext(IReadOnlyDictionary<string, JObject> values)
    {
        var scopes = values.Values.Select(v => v["value"] as JObject)
            .Select(row => row == null ? null : (string?)row["projectName"] ?? (string?)row["parentName"])
            .Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase).Take(2).ToArray();
        if (scopes.Length == 1)
        {
            var scope = new FriendlyResponsePresenter().Present(scopes[0]);
            return StudioStrings.Get("Question.DocumentContext", scope);
        }
        return StudioStrings.Get("Question.DocumentContextGeneric");
    }

    public QuestionAnswer Answer(string text = "", IEnumerable<string>? selectedKeys = null, ChatAttachment? image = null, CultureInfo? culture = null)
    {
        var data = new JObject { ["status"] = "answered", ["kind"] = Kind };
        string summary;
        if (Kind == "select")
        {
            var requestedKeys = (selectedKeys ?? []).ToHashSet(StringComparer.Ordinal);
            if (requestedKeys.Any(key => !values.ContainsKey(key))) throw new ArgumentException(StudioStrings.Get("Question.SelectRequired"));
            // Selection order follows the native list, never the order of mouse clicks or a HashSet.
            var keys = Choices.Where(choice => requestedKeys.Contains(choice.Key)).Select(choice => choice.Key).ToArray();
            if (keys.Length == 0 || (!Multiple && keys.Length != 1) || keys.Any(key => !values.ContainsKey(key)))
                throw new ArgumentException(StudioStrings.Get("Question.SelectRequired"));
            data["selected"] = new JArray(keys.Select(key => values[key].DeepClone()));
            summary = string.Join(", ", keys.Select(key => Choices.Single(c => c.Key == key).Label));
        }
        else if (Kind == "image")
        {
            if (image?.IsImage != true) throw new ArgumentException(StudioStrings.Get("Question.ImageRequired"));
            ChatAttachments.Validate([image]);
            data["image"] = new JObject { ["name"] = image.Name, ["mediaType"] = image.MediaType, ["sha256"] = image.Sha256 };
            summary = image.Name;
        }
        else if (Kind == "color")
        {
            if (!Regex.IsMatch(text.Trim(), "^#[0-9a-fA-F]{6}$")) throw new ArgumentException(StudioStrings.Get("Question.ColorRequired"));
            var hex = text.Trim().ToUpperInvariant();
            data["value"] = new JObject { ["hex"] = hex, ["r"] = Convert.ToInt32(hex[1..3], 16), ["g"] = Convert.ToInt32(hex[3..5], 16), ["b"] = Convert.ToInt32(hex[5..7], 16) };
            summary = hex;
        }
        else if (Kind is "integer" or "decimal")
        {
            // Thousands separators are deliberately disallowed: 1,234 must not silently become 1234.
            var style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
            if (!decimal.TryParse(text.Trim(), style, culture ?? CultureInfo.CurrentCulture, out var number) &&
                !decimal.TryParse(text.Trim(), style, CultureInfo.InvariantCulture, out number))
                throw new ArgumentException(StudioStrings.Get("Question.NumberRequired"));
            if (Kind == "integer" && (decimal.Truncate(number) != number || number < int.MinValue || number > int.MaxValue))
                throw new ArgumentException(StudioStrings.Get("Question.IntegerRequired"));
            if (Minimum.HasValue && number < Minimum || Maximum.HasValue && number > Maximum)
                throw new ArgumentException(StudioStrings.Get("Question.OutOfRange"));
            data["value"] = Kind == "integer" ? new JValue((int)number) : new JValue(number);
            if (Unit.Length > 0) data["unit"] = Unit;
            summary = number.ToString(culture ?? CultureInfo.CurrentCulture) + (Unit.Length > 0 ? " " + Unit : "");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4000) throw new ArgumentException(StudioStrings.Get("Question.TextRequired"));
            data["value"] = text;
            summary = text;
        }
        if (data.ToString(Formatting.None).Length > 60000) throw new ArgumentException(StudioStrings.Get("Question.SelectionTooLarge"));
        return new QuestionAnswer(data, summary, Kind == "image" ? image : null);
    }
}

/// <summary>A turn-local store; model prose cannot create or rename TopSolid choices.</summary>
internal sealed class QuestionSources
{
    internal const string ToolName = "studio_ask_user";
    private sealed record Source(string Tool, JObject Arguments, JToken Data);
    private readonly Dictionary<string, Source> receipts = new(StringComparer.Ordinal);
    private readonly HashSet<string> duplicateIds = new(StringComparer.Ordinal);
    private readonly FriendlyResponsePresenter presenter = new();

    internal void Capture(string callId, string tool, JObject arguments, McpToolResult result)
    {
        if (!tool.StartsWith("topsolid_", StringComparison.Ordinal)) return;
        if (receipts.Remove(callId)) duplicateIds.Add(callId);
        if (duplicateIds.Contains(callId) || result.IsError) return;
        var json = ToolResultContext.Serialize(result);
        if (json.Length > 64000) return;
        presenter.ObserveToolResult(json, arguments, tool);
        JToken? data = result.StructuredContent;
        if (data == null)
            foreach (var block in result.Content.OfType<JObject>())
                if ((string?)block["type"] == "text" && block["text"]?.Type == JTokenType.String)
                    try { data = JToken.Parse((string)block["text"]!); break; } catch (JsonException) { }
        if (data != null) receipts[callId] = new Source(tool, (JObject)arguments.DeepClone(), data.DeepClone());
    }

    internal UserQuestion Create(JObject input, bool browse = false)
    {
        var allowed = new[] { "question", "kind", "itemKind", "multiple", "unit", "minimum", "maximum", "sources", "choices" };
        if (input.Properties().Any(p => !allowed.Contains(p.Name))) throw new ArgumentException("Unknown question field.");
        var title = RequiredText(input, "question", 240);
        var kind = RequiredText(input, "kind", 16);
        if (!new[] { "select", "text", "integer", "decimal", "image", "color" }.Contains(kind)) throw new ArgumentException("Unsupported question kind.");
        var itemKind = input["itemKind"] == null ? "option" : RequiredText(input, "itemKind", 24);
        if (!Kinds.Contains(itemKind)) throw new ArgumentException("Unsupported item kind.");
        if (input["multiple"] is { Type: not JTokenType.Boolean }) throw new ArgumentException("multiple must be boolean.");
        var unit = input["unit"] == null ? "" : RequiredText(input, "unit", 32);
        decimal? Bound(string key) => input[key] == null ? null : input[key]!.Type is JTokenType.Integer or JTokenType.Float &&
            decimal.TryParse(input[key]!.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : throw new ArgumentException("Invalid numeric bound.");
        var minimum = Bound("minimum"); var maximum = Bound("maximum");
        if (minimum > maximum) throw new ArgumentException("Minimum exceeds maximum.");
        var choices = new List<QuestionChoice>(); var values = new Dictionary<string, JObject>();
        if (kind == "select")
        {
            if (input["sources"] is JArray sources)
            {
                // Some providers materialize an omitted optional array as [] even
                // when they correctly chose receipt-backed sources. Treat an empty
                // choices array as omitted, but reject actual mixed choice payloads.
                if (input["choices"] is JArray suppliedChoices && suppliedChoices.Count > 0 || sources.Count is < 1 or > 24)
                    throw new ArgumentException("Use sources OR general choices.");
                foreach (var source in sources.OfType<JObject>())
                {
                    if (source.Properties().Any(p => p.Name is not ("toolCallId" or "path" or "itemKind" or "editableOnly"))) throw new ArgumentException("Unknown selection source field.");
                    if (source["editableOnly"] is { Type: not JTokenType.Boolean }) throw new ArgumentException("editableOnly must be boolean.");
                    var sourceKind = source["itemKind"] == null ? itemKind : RequiredText(source, "itemKind", 24);
                    if (!Kinds.Contains(sourceKind)) throw new ArgumentException("Unsupported source item kind.");
                    var id = RequiredText(source, "toolCallId", 256);
                    if (!receipts.TryGetValue(id, out var receipt)) throw new ArgumentException("Selection source must be a successful tool call from this turn. Query it again.");
                    var path = source["path"]?.Type == JTokenType.String ? (string)source["path"]! : throw new ArgumentException("Provide a JSON pointer path, or empty string for the root.");
                    var rows = Pointer(receipt.Data, path);
                    var tokens = rows is JArray array ? array.ToArray() : [rows];
                    foreach (var row in tokens)
                    {
                        if ((bool?)source["editableOnly"] == true && (row is not JObject editable || (bool?)editable["editSupported"] != true || (bool?)editable["readOnly"] != false)) continue;
                        if (choices.Count >= 500) throw new ArgumentException("Narrow the selection to at most 500 items.");
                        if (row.Type == JTokenType.Null || row is JObject failed && (bool?)failed["isError"] == true) continue;
                        if (sourceKind != "option" && row is not JObject) throw new ArgumentException("Read named object details before asking for a TopSolid selection.");
                        var key = "choice-" + choices.Count;
                        // A generic assistant itemKind must not erase native CAM identity. Parameter
                        // rows may mention their parent operation, so they retain their own kind.
                        var rowKind = receipt.Tool switch
                        {
                            "topsolid_list_cam_tools" => "tool",
                            "topsolid_list_sketches2d" or "topsolid_list_sketches3d" => "sketch",
                            "topsolid_list_document_summaries" or "topsolid_list_documents" or "topsolid_list_loaded_documents" => "document",
                            "topsolid_list_pdm_children" when row is JObject child &&
                                string.Equals((string?)child["kind"], "document", StringComparison.OrdinalIgnoreCase)
                                => "document",
                            "topsolid_list_projects" => "project",
                            "topsolid_list_libraries" => "library",
                            "topsolid_list_shape_summaries" => "shape",
                            _ when row is JObject operationRow && operationRow["parameter"] == null &&
                                (receipt.Tool is "topsolid_list_cam_operations" or "topsolid_list_cam_operation_summaries" or "topsolid_list_cam_scenario" ||
                                 operationRow["operationType"]?.Type == JTokenType.String ||
                                 operationRow["operation"] is JObject && operationRow["operationName"]?.Type == JTokenType.String)
                                => "operation",
                            _ => sourceKind
                        };
                        var label = Label(row, rowKind, choices.Count + 1);
                        var toolText = rowKind == "operation" ? CamDisplay.ToolText(row) : null;
                        if (rowKind == "operation" && string.IsNullOrWhiteSpace(toolText) && (bool?)row["hasTool"] == true)
                            toolText = StudioStrings.Get("Question.ToolUnavailable");
                        var detail = Details(row, label, rowKind == "operation");
                        var icon = receipt.Tool == "topsolid_list_pdm_children" && (string?)row["kind"] == "folder" ? "folder" :
                            rowKind == "tool" ? TopSolidIcons.ToolFunctionKey(row) : rowKind == "operation" ? TopSolidIcons.OperationKey(row) : rowKind == "camParameter" ? TopSolidIcons.CamCategoryKey(row) :
                            rowKind is "document" or "option" ? TopSolidIcons.DocumentKey(row) : null;
                        choices.Add(new(key, label, detail, rowKind, label + " " + detail + " " + toolText, icon, toolText,
                            string.IsNullOrWhiteSpace(toolText) ? null : TopSolidIcons.ToolFunctionKey(row),
                            rowKind == "operation" ? OperationToolGroups.Identity(row) : null));
                        values.Add(key, new JObject { ["sourceTool"] = receipt.Tool, ["sourceArguments"] = receipt.Arguments.DeepClone(), ["value"] = row.DeepClone() });
                    }
                }
                if (sources.Any(s => s is not JObject)) throw new ArgumentException("Invalid selection source.");
            }
            else if (input["choices"] is JArray options && itemKind == "option" && options.Count is > 0 and <= 32)
            {
                foreach (var option in options)
                {
                    if (option.Type != JTokenType.String || (string?)option is not { } optionText || string.IsNullOrWhiteSpace(optionText) || optionText.Length > 160)
                        throw new ArgumentException("General choices must be short text labels.");
                    var key = "choice-" + choices.Count; var label = presenter.Present(optionText);
                    choices.Add(new(key, label, "", "option", label)); values.Add(key, new JObject { ["value"] = option.DeepClone() });
                }
            }
            else throw new ArgumentException("Object selections require sources; general options may use choices.");
                if (choices.Count == 0 && !browse) throw new ArgumentException("No selectable items. Read valid named objects before asking.");
        }
        else if (input["sources"] != null || input["choices"] != null || (bool?)input["multiple"] == true)
            throw new ArgumentException("Selection fields are only valid for select questions.");
        return new UserQuestion(presenter.Present(title), kind, itemKind, (bool?)input["multiple"] == true,
            presenter.Present(unit), minimum, maximum, choices.AsReadOnly(), values);
    }

    internal string ExposeSource(string callId, string content)
    {
        if (!receipts.ContainsKey(callId)) return content;
        // Ollama matches results by name/order and omits call IDs on the wire.
        // Publish this client-owned reference in content so every provider can cite it.
        var envelope = JObject.Parse(content); envelope["studioSourceId"] = callId;
        return envelope.ToString(Formatting.None);
    }

    private string Label(JToken row, string kind, int ordinal)
    {
        if (row is JObject obj)
        {
            // Tool labels are native library names, including pocket and dimensions. Do not
            // run names such as "Tool 1" through the prose identifier-redaction rules.
            if (kind == "tool")
                foreach (var field in new[] { "toolDisplayName", "toolDefinitionName", "friendlyName", "name" })
                    if (obj[field]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string?)obj[field]))
                        return ((string)obj[field]!).Trim();
            foreach (var field in kind == "operation"
                ? new[] { "operationName", "displayName", "localizedName", "friendlyName", "sketchName", "label", "name", "Name", "description" }
                : new[] { "displayName", "localizedName", "friendlyName", "label", "simpleName", "name", "Name" })
                if (obj[field]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string?)obj[field]))
                {
                    var name = ((string)obj[field]!).Trim();
                    if (kind == "camParameter" && name.Contains('@')) name = name.Split('@')[0];
                    if (!long.TryParse(name, out _) && !Guid.TryParse(name, out _)) return presenter.Present(name);
                }
        }
        if (kind == "option" && row is JValue) return presenter.Present(row.ToString());
        return StudioStrings.Get("Question.Unnamed", StudioStrings.Get("Question.Kind." + kind), ordinal);
    }

    private string Details(JToken row, string label, bool operation = false)
    {
        if (row is not JObject obj) return "";
        var fields = new[] { "projectName", "documentName", "operationName", "sketchName", "parentName", "toolName", "path", "extension", "revision", "displayValue", "unitSymbol", "description", "geometryType", "type", "plane", "creationDate", "modificationDate" };
        var parts = fields.Where(key => !operation || key != "toolName").Select(key => obj[key]).Where(v => v?.Type == JTokenType.String).Select(v => presenter.Present((string)v!).Trim()).Where(s => s.Length > 0 && s != label).ToList();
        if (operation && obj["operationType"]?.Type == JTokenType.String)
            parts.Insert(0, CamDisplay.Operation((string?)obj["operationType"]));
        if (obj["categories"] is JArray categories)
            parts.Insert(0, string.Join(" / ", categories.Values<string>().OfType<string>().Select(FriendlyResponsePresenter.FriendlyLabel)));
        // Geometry measurements can distinguish unnamed topology without displaying handles.
        foreach (var field in new[] { "position", "center", "length", "radius", "area" })
            if (obj[field] is { } value && value.ToString(Formatting.None).Length < 120)
                parts.Add(FriendlyResponsePresenter.FriendlyLabel(field) + ": " + presenter.Present(value.ToString(Formatting.None)) + " (SI)");
        return string.Join(" · ", parts.Distinct()).Trim();
    }

    private static string RequiredText(JObject obj, string key, int maximum) => obj[key]?.Type == JTokenType.String &&
        (string)obj[key]! is { } text && !string.IsNullOrWhiteSpace(text) && text.Length <= maximum ? text : throw new ArgumentException("Invalid " + key + ".");

    private static JToken Pointer(JToken root, string path)
    {
        if (path.Length == 0) return root;
        if (!path.StartsWith('/') || path.Length > 512) throw new ArgumentException("Invalid selection JSON pointer.");
        foreach (var segment in path[1..].Split('/'))
        {
            var key = segment.Replace("~1", "/").Replace("~0", "~");
            root = root is JObject obj ? obj[key] ?? throw new ArgumentException("Selection path not found.") :
                root is JArray array && int.TryParse(key, out var index) && index >= 0 && index < array.Count ? array[index] : throw new ArgumentException("Selection path not found.");
        }
        return root;
    }

    internal static readonly string[] Kinds = ["option", "project", "library", "document", "parameter", "operation", "machine", "camParameter", "element", "edge", "point", "curve", "surface", "shape", "part", "tool", "sketch"];
    internal static readonly McpToolDefinition Definition = new()
    {
        Name = ToolName, Description = "Ask the user one necessary question in an icon-card dialog. Selection is not authorization to modify CAD. For real objects use sources from successful tool calls in this turn: toolCallId is the receipt's studioSourceId. JSON pointer paths address the unwrapped result (e.g. /items). All selection values are preserved exactly. Text choices are only for general options, never invented object identities.",
        Annotations = new JObject { ["readOnlyHint"] = true },
        InputSchema = JObject.Parse("""
        {"type":"object","properties":{
          "question":{"type":"string","maxLength":240},
          "kind":{"type":"string","enum":["select","text","integer","decimal","image","color"]},
          "itemKind":{"type":"string","enum":["option","project","library","document","parameter","operation","machine","camParameter","element","edge","point","curve","surface","shape","part","tool","sketch"]},
          "sources":{"type":"array","minItems":1,"maxItems":24,"items":{"type":"object","properties":{"toolCallId":{"type":"string"},"path":{"type":"string","description":"JSON pointer to rows, an individual object, or enum choices in the unwrapped result."},"itemKind":{"type":"string","description":"Optional per-source kind for mixed project/library/document choices."},"editableOnly":{"type":"boolean","description":"Only include rows with verified editSupported=true and readOnly=false."}},"required":["toolCallId","path"],"additionalProperties":false}},
          "choices":{"type":"array","minItems":1,"maxItems":32,"items":{"type":"string"}},
          "multiple":{"type":"boolean"},"unit":{"type":"string","maxLength":32},
          "minimum":{"type":"number"},"maximum":{"type":"number"}
        },"required":["question","kind"],"additionalProperties":false}
        """ )
    };

    internal const string Instructions =
        "For genuinely missing user data or an ambiguous target, CALL studio_ask_user instead of asking in chat. Use the user's language. Do not ask for known data or ask again for change approval. For an object selection, use receipt-backed sources; never answer an explicit select/choose/pick/dialog request with raw prose or an ID. " +
        "Read candidate objects first. Set sources.toolCallId to studioSourceId from the successful receipt in THIS turn; path is a JSON pointer into its unwrapped data, e.g. /items. Never prefix it with /structuredContent, and never append /operation to an array. Use /items for complete operation cards or /items/0 for one card. Fetch all pages or narrow the list before asking; the dialog searches only supplied items. Never claim a dialog was shown after a tool error; correct the arguments and retry. " +
        "Each source can specify itemKind for a combined project/library/document picker. Read names and distinguishing parent/location/geometry context for duplicate or unnamed items. Use itemKind=camParameter and sources.editableOnly=true to choose editable operation parameters, sources at allowedValues for native enum choices. Honor each requested selection before asking its value. Omit choices when using sources; an empty optional choices array is tolerated but is not a general-choice payload. " +
        "Use integer/decimal with explicit input unit and only verified limits; returned numbers remain in that unit, so convert per the action schema. Image questions attach an explicitly chosen image as reference data. " +
        "Selection answers contain original rows and scope arguments, not approval; revalidate the target through normal prepare/confirmation before changes. Ask one question per model round and never issue a dependent action in the same batch. Cancellation ends the workflow. ";

    internal static UserQuestion Projects(IEnumerable<JObject> rows, string? projectName)
    {
        var sources = new QuestionSources();
        sources.Capture("project-lookup", "topsolid_get_document_creation_context", new JObject { ["projectName"] = projectName },
            new McpToolResult { StructuredContent = new JObject { ["items"] = new JArray(rows.Select(r => r.DeepClone())) } });
        return sources.Create(new JObject { ["question"] = StudioStrings.Get("Question.ChooseProject"), ["kind"] = "select", ["itemKind"] = "project",
            ["sources"] = new JArray(new JObject { ["toolCallId"] = "project-lookup", ["path"] = "/items" }) });
    }
}
