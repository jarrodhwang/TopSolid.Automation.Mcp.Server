using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

// Keep provider payloads bounded as the MCP catalog grows. This selector only exposes
// discovered schemas; all CAD execution still uses MCP and its confirmation path.
internal sealed class ToolExposure
{
    internal const string SelectorName = "studio_select_tools";
    internal const int MaximumTools = 96;
    internal const int InitialTools = 24;
    internal static bool NeedsWorkflowHistory(string request)
    {
        const RegexOptions flags = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (Regex.IsMatch(request, @"\b(it|that|those|same|previous|again|instead)\b|다시|이전|그\s", flags)) return true;
        return !Regex.IsMatch(request, "^\\s*[\"“']?\\s*(?:please\\s+)?(?:show|list|let me|ask me|create|make|draw|read|change|set)\\b", flags);
    }
    private readonly McpToolDefinition[] discovered;
    private readonly bool compact;
    private readonly McpToolDefinition selector;
    private McpToolDefinition[] active;
    private static readonly HashSet<string> Core = new(StringComparer.Ordinal) {
        "topsolid_get_status", "topsolid_get_active_document", "topsolid_get_document_info", "topsolid_get_capabilities",
        "topsolid_list_projects", "topsolid_list_libraries", "topsolid_list_pdm_children", "topsolid_get_current_project", "topsolid_get_user_selection", "topsolid_get_object_model", "topsolid_find_pdm_documents", "topsolid_find_named_elements", "topsolid_resolve_pdm_documents" };

    public ToolExposure(McpToolDefinition[] discovered, string request = "", bool compact = false, string? currentRequest = null, StudioContextOptions? context = null)
    {
        // Sections are a separate explicit request, never an ordinary modeling prerequisite.
        var explicitSection = ExplicitSectionRequested(currentRequest ?? request);
        this.discovered = discovered = discovered.Where(t => (t.Name != "topsolid_create_sketch_section" || explicitSection) && t.Name != "topsolid_apply_cam_color_plan" && t.Name != CamAutomationPreparation.ExecuteMethod).ToArray();
        this.compact = compact;
        if (discovered.Any(t => t.Name == SelectorName)) throw new InvalidOperationException("MCP tool name conflicts with Studio's schema selector.");
        selector = new McpToolDefinition { Name = SelectorName,
            Description = "List tool names/descriptions with category, OR load schemas with exact names. Use one field only. Discovery is local and does not execute/approve any TopSolid action. After loading schemas call the tools on the next request.",
            InputSchema = JObject.Parse("{\"type\":\"object\",\"properties\":{\"category\":{\"type\":\"string\",\"description\":\"Category from the system catalog; lists tools without loading schemas.\"},\"names\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":32,\"items\":{\"type\":\"string\"}}},\"additionalProperties\":false}"),
            Annotations = new JObject { ["readOnlyHint"] = true } };
        bool Has(string pattern) => Regex.IsMatch(request, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var sketch = Has(@"\b(sketch(?:es)?|skteches?|circles?|rectangles?|slots?|polylines?|contours?|profiles?|parabolas?|b-?splines?|arcs?|ellips[ei]s|ellipse|stars?|trees?|hearts?|draw)\b|스케치|원형|슬롯|포물선|스플라인|타원|별|하트|그려");
        var document = Has(@"\b(documents?|parts?|files?|assembly|assemblies|drafting|drawing|Top[A-Za-z0-9]+)\b|문서|부품|파트|도큐먼트|도면|조립");
        var pdm = Has(@"\b(project|projects|pdm|library|libraries|folder)\b|프로젝트|라이브러리");
        var create = Has(@"\b(create|make|add|new)\b|생성|만들");
        var createDocument = Has(@"\b(?:create|make|add|new)\s+(?:(?:a|an|one|empty|plain|native|new|more|another)\s+)*(?:part|document|file|assembly)\b|새\s*(?:문서|부품|파트)|(?:문서|부품|파트)(?:를|을)?\s*(?:생성|만들)");
        var preferred = new List<string>();
        // Cutting conditions normally belong to an operation. The separate
        // library documents are selected only when the user asks for them.
        var cam = Has(@"\b(cam|machining|feed[ -]?rate|feed|rpm|spindle|coolant|cutting\s+(?:conditions?|speed)|toolpath|stepover|stepdown)\b|가공|절삭|이송|피드|주축|회전수|절삭유");
        var simulation = Has(@"\b(simulat(?:e|ion|ing)?|verif(?:y|ication|ying)?)\b|시뮬레이션|검증");
        cam |= simulation;
        var cuttingDocument = Regex.IsMatch(currentRequest ?? request, @"\bcutting[ -]?conditions?\s+(?:documents?|abacus|library)\b|\b(?:documents?|abacus|library)\s+(?:of\s+|for\s+)?cutting[ -]?conditions?\b|절삭\s*조건\s*(?:문서|도큐먼트|라이브러리|아바커스)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cam |= cuttingDocument;
        if (cam) {
            preferred.AddRange(["topsolid_list_cam_operation_summaries", "topsolid_list_cam_parameters", "topsolid_get_cam_parameter_value", "topsolid_get_active_document"]);
            if (simulation)
                preferred.InsertRange(0, ["topsolid_simulate_cam_operation", "topsolid_verify_cam_operation", "topsolid_verify_all_cam_operations"]);
            if (Has(@"\b(set|change|modify|edit|update|increase|decrease|reduce)\b|수정|변경|바꿔|높여|낮춰|줄여")) preferred.Add("topsolid_set_cam_parameter_value");
            if (cuttingDocument) preferred.InsertRange(0, ["topsolid_get_cutting_conditions_document", "topsolid_list_cutting_conditions_documents", "topsolid_get_cutting_conditions_abacus"]);
            preferred.AddRange(["topsolid_get_cam_operation_info", "topsolid_get_user_selection"]);
        }
        if (explicitSection) preferred.AddRange(["topsolid_create_sketch_section", "topsolid_get_sketch2d_context", "topsolid_read_sketch2d_geometry"]);
        var heart = Has(@"\bhearts?\b|하트");
        var reference = Has(@"\b(reference|relative|linked|selected|selection)\b|참조|기준|선택");
        var parameter = Has(@"\b(parameters?|parametric|formulas?|expressions?|enumerations?|tolerances?)\b|파라미터|매개변수|매개\s*변수|수식|공차")
            && !Has(@"\b(?:no|without)\s+(?:named\s+)?parameters?\b|\b(?:do not|don't)\s+(?:create|use)\s+parameters?\b|(?:파라미터|매개변수)\s*(?:없이|(?:생성\s*(?:및\s*저장은\s*)?)?하지\s*마)");
        var entity = Has(@"\b(entit(?:y|ies)|elements?|publishings?|shortcuts?)\b|엔티티|요소");
        var entityFolder = (parameter || entity) && Has(@"\bfolders?\b|폴더");
        var cylinder = Has(@"\bcylinders?\b|실린더|원기둥");
        var coloring = Has(@"\b(colou?r\w*|red|blue|green|yellow|orange|purple|black|white|paint)\b|색상|색으로|빨간|빨갛|빨강|붉은|파란|파랗|파랑|초록|녹색|노란|노랑|검정|검은|하얀|흰색");
        var sketch3d = sketch && Has(@"\b3\s*d\s*(sketch|curve|polyline|arc|circle)|\b(sketch|curve)\s*(in\s*)?3\s*d\b|3차원\s*스케치|3D\s*스케치");
        var feature = cylinder || Has(@"\b(extrud\w*|revolv\w*|revolution|loft\w*|fillets?|chamfers?|booleans?|boss|pockets?|trim|modeling)\b|돌출|회전체|회전\s*형상|필렛|모따기|포켓|불리언");
        // Prefer exact operation workflows before broad entity/parameter catalogs.
        // Color words describe appearance unless an actual parameter is requested.
        if (cylinder) preferred.AddRange(["topsolid_create_cylinder", "topsolid_get_active_document", "topsolid_list_named_elements"]);
        if (coloring && (!parameter || cylinder || sketch || Has(@"\b(shape|surface|body)\b|형상|표면"))) {
            preferred.AddRange(["topsolid_set_entity_colors", "topsolid_get_active_document", "topsolid_list_named_elements"]);
            if (Has(@"\bfaces?\b|면\s*색|면을|면에")) preferred.AddRange(["topsolid_color_shape_faces", "topsolid_list_shape_faces"]);
        }
        if (sketch3d) preferred.AddRange(["topsolid_create_sketch3d_curves", "topsolid_get_active_document"]);
        if (feature) preferred.Add("topsolid_get_modeling_guide");
        if (parameter && !cam) {
            preferred.AddRange(["topsolid_list_parameter_values", "topsolid_inspect_parameters"]);
            if (Has(@"\b(formulas?|expressions?|linked|reference)\b|수식|참조|연결")) preferred.Add(create ? "topsolid_create_parameter_expressions" : "topsolid_set_parameter_expressions");
            else if (create) preferred.Add("topsolid_create_parameters");
            else if (Has(@"\b(set|change|modify|edit|update)\b|수정|변경|바꿔")) preferred.Add("topsolid_set_parameter_values");
            if (Has(@"\b(enum\w*|choices?|colors?|codes?)\b|열거|색상|코드")) preferred.Add("topsolid_get_parameter_choices");
            preferred.Add("topsolid_get_active_document");
        }
        if (entity || entityFolder) {
            preferred.AddRange(["topsolid_list_named_elements", "topsolid_inspect_entity_structure", "topsolid_list_entity_children"]);
            if (Has(@"\b(propert\w*|metadata)\b|속성")) preferred.Add("topsolid_read_element_properties");
            if (Has(@"\b(color|transparen\w*|rename|description|comment)\b|색상|투명|이름\s*변경")) preferred.Add("topsolid_update_elements");
        }
        if (entityFolder) preferred.Add(create ? "topsolid_create_entity_folders" : "topsolid_move_entities");
        if ((entity || parameter) && Has(@"\b(delete|remove)\b|삭제")) preferred.Add("topsolid_delete_elements");
        if (pdm && (sketch || document)) preferred.Add("topsolid_get_modeling_context");
        if (!cylinder && Has(@"\b(extrud\w*|solid)\b|돌출")) preferred.AddRange(["topsolid_extrude_sketch", "topsolid_get_sketch2d_context"]);
        if (!cylinder && Has(@"\b(revolv\w*|revolution)\b|회전체|회전\s*형상")) preferred.AddRange(["topsolid_revolve_sketch", "topsolid_get_sketch2d_context"]);
        if (heart && !reference) preferred.Add("topsolid_create_heart_sketch");
        if (Has(@"\bcheck[ -]?in\b|체크인")) preferred.AddRange(["topsolid_check_in_pdm_objects", "topsolid_get_document_creation_context"]);
        if (Has(@"\bsav(?:e|ing)\b|저장") && !Has(@"\b(?:do not|don't|no)\s+save\b|\bwithout\s+saving\b|저장(?:은|을)?\s*(?:하지|안|금지)")) preferred.AddRange(["topsolid_save_documents", "topsolid_save_document"]);
        if (sketch && Has(@"\b(selected|selection)\b|선택")) preferred.Add("topsolid_get_user_selection");
        // A workflow crosses categories: creating a part is a PDM action, then
        // opening/saving it uses Documents and drawing it uses Sketch2D.
        if (createDocument) preferred.AddRange(["topsolid_get_document_creation_context", "topsolid_create_part_document", "topsolid_create_document", "topsolid_open_document", "topsolid_save_document", "topsolid_list_document_types"]);
        if (Has(@"\btemplates?\b|템플릿") && !Has(@"\b(without|no)\s+(?:a\s+)?template\b|템플릿\s*없이"))
            preferred.AddRange(["topsolid_get_template_projects", "topsolid_list_pdm_children", "topsolid_get_pdm_object_info"]);
        if (pdm && !(compact && (sketch || document))) preferred.AddRange(["topsolid_list_projects", "topsolid_list_libraries"]);
        if (pdm && create && !entityFolder && !(compact && sketch)) preferred.AddRange(["topsolid_create_project", "topsolid_create_folder"]);
        if (sketch && !sketch3d && !(compact && heart && !reference)) {
            if (!(compact && heart && !reference)) preferred.Add("topsolid_create_sketches2d");
            preferred.AddRange(["topsolid_get_sketch2d_context", "topsolid_read_sketch2d_geometry"]);
            if (!compact || reference) preferred.Add("topsolid_transform_sketch2d_points");
            if (!compact) preferred.AddRange(["topsolid_create_sketch_profiles", "topsolid_create_circle2d", "topsolid_create_contour2d"]);
        }
        if (document || sketch) {
            preferred.AddRange(["topsolid_get_active_document", "topsolid_get_document_info"]);
            if (!compact) preferred.AddRange(["topsolid_find_pdm_documents", "topsolid_find_named_elements"]);
        }
        // Explicit request matches were added first; context seeds never outrank them.
        if (context?.Cam == true) preferred.AddRange(["topsolid_list_cam_operation_summaries", "topsolid_list_cam_parameters", "topsolid_list_cam_tools", "topsolid_list_cam_parts", "topsolid_inspect_cam_color_geometry"]);
        if (context?.Cad == true) preferred.AddRange(["topsolid_get_sketch2d_context", "topsolid_get_modeling_guide", "topsolid_list_named_elements", "topsolid_list_parameter_values"]);
        int Rank(McpToolDefinition t) {
            var category = (string?)t.Metadata["topsolid/category"];
            var preferredIndex = preferred.IndexOf(t.Name);
            if (preferredIndex >= 0) return 1000 - preferredIndex;
            if (t.Name.Contains("cutting_conditions", StringComparison.Ordinal) && !cuttingDocument) return 0;
            if (t.Name == "topsolid_get_template_projects") return 0;
            if (t.Name is "topsolid_get_capabilities" or "topsolid_get_active_document" or "topsolid_get_document_info" or "topsolid_find_pdm_documents" or "topsolid_resolve_pdm_documents") return 8;
            if (sketch && t.Name is "topsolid_create_circle2d" or "topsolid_create_rectangle2d" or "topsolid_create_sketch_profiles" or "topsolid_read_sketch2d_geometry") return 7;
            if (sketch && category == "Sketch2D" || pdm && category == "Pdm") return 6;
            return Core.Contains(t.Name) ? 5 : IsBatch(t.Name) ? 3 : t.RequiresConfirmation ? 2 : 1;
        }
        active = discovered.OrderByDescending(Rank).ToArray();
        if (IsLimited) {
            if (compact) {
                // Avoid sending several overlapping large sketch schemas by default.
                active = active.Where(t => preferred.Contains(t.Name) || t.Name is "topsolid_get_status" or "topsolid_get_active_document" or "topsolid_get_document_info").ToArray();
                if (active.Length == 0) active = discovered.Take(9).ToArray();
                var chars = 0;
                active = active.Where(t => { var size = t.InputSchema.ToString(Newtonsoft.Json.Formatting.None).Length + t.Description.Length;
                    if (chars > 0 && chars + size > 16000) return false; chars += size; return true; }).Take(9).ToArray();
            } else active = active.Take(InitialTools - 1).ToArray();
        }
    }
    private bool IsLimited => discovered.Length > (compact ? 10 : InitialTools);
    internal static bool ExplicitSectionRequested(string text)
    {
        const RegexOptions flags = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (Regex.IsMatch(text, @"\b(?:no|without|unnecessary)\s+(?:a\s+|any\s+)?(?:sketch\s+)?sections?\b|\b(?:do\s+not|don't|never)\s+(?:create|add|make|build)\s+(?:a\s+|any\s+)?(?:sketch\s+)?sections?\b|(?:섹션|단면).{0,16}(?:없이|필요\s*없|만들지|생성하지|불필요)", flags)) return false;
        return Regex.IsMatch(text, @"\b(?:create|add|make|build)\b[^.!?\r\n]{0,80}\b(?:sketch\s+)?sections?\b|(?:섹션|스케치\s*단면).{0,30}(?:만들|생성|추가)|(?:만들|생성|추가).{0,30}(?:섹션|스케치\s*단면)", flags);
    }
    public McpToolDefinition[] Active => IsLimited ? active.Append(selector).ToArray() : active;
    public bool IsSelector(string name) => IsLimited && name == SelectorName;
    public string Catalog => !IsLimited ? "" : compact ?
        "Other MCP tools remain available. studio_select_tools(category) lists names locally; then studio_select_tools(names) loads schemas. Categories: " +
        string.Join(", ", discovered.Select(t => (string?)t.Metadata["topsolid/category"] ?? "Other").Distinct().OrderBy(c => c)) :
        "Full registered MCP catalog (names only; includes tools whose schemas are not yet loaded). " +
        "Use studio_select_tools with these exact names when their schemas are omitted. Do not claim a capability is missing because it is outside the current schema subset. " +
        "Registration does not prove module connection or licensing. Every change still requires user confirmation.\n" +
        string.Join("\n", discovered.GroupBy(t => (string?)t.Metadata["topsolid/category"] ?? "Other")
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + ": " + string.Join(", ", g.Select(t => t.Name))));
    private static bool IsBatch(string name) => name.Contains("summaries", StringComparison.Ordinal) || name.Contains("_values", StringComparison.Ordinal)
        || name.Contains("_geometry", StringComparison.Ordinal) || name.Contains("_inspect_", StringComparison.Ordinal) || name == "topsolid_list_named_elements"
        || name == "topsolid_list_assembly_occurrences" || name == "topsolid_create_sketch_profiles" || name.EndsWith("_sections", StringComparison.Ordinal);

    public McpToolResult Select(JObject arguments)
    {
        if (arguments.Count == 1 && arguments["category"]?.Type == JTokenType.String) {
            var entries = discovered.Where(t => string.Equals((string?)t.Metadata["topsolid/category"] ?? "Other", (string?)arguments["category"], StringComparison.OrdinalIgnoreCase)).ToArray();
            if (entries.Length == 0) return McpToolResult.Error("Unknown category. Use a category from the system catalog.");
            return new McpToolResult { StructuredContent = new JObject { ["tools"] = new JArray(entries.Select(t => new JObject { ["name"] = t.Name,
                ["description"] = t.Description.Length > 240 ? t.Description[..240] : t.Description })), ["message"] = "Select exact names to load schemas; no TopSolid call was executed." } };
        }
        if (arguments.Count != 1 || arguments["names"] is not JArray names || names.Count is < 1 or > 32 || names.Any(v => v.Type != JTokenType.String))
            return McpToolResult.Error("Provide only category, or only names: an array of exact discovered tool names.");
        var requested = names.Select(v => (string)v!).Distinct(StringComparer.Ordinal).ToArray();
        if (compact && requested.Length > 15) return McpToolResult.Error("Load at most 15 tool schemas at a time for local models.");
        if (requested.Any(name => !discovered.Any(t => t.Name == name))) return McpToolResult.Error("Unknown tool name. Use exact names from the full registered catalog in the system message; no tools were selected.");
        active = requested.Select(name => discovered.Single(t => t.Name == name)).Concat(compact ? [] : discovered.Where(t => Core.Contains(t.Name)))
            .Concat(active).DistinctBy(t => t.Name).Take(compact ? 15 : MaximumTools - 1).ToArray();
        return new McpToolResult { Content = new JArray(new JObject { ["type"] = "text", ["text"] = new JObject {
            ["availableNextRequest"] = new JArray(requested), ["message"] = "Selected tool schemas are available on the next model request. No TopSolid operation was executed." }.ToString(Newtonsoft.Json.Formatting.None) }) };
    }
}
