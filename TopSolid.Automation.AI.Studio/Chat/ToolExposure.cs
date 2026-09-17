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
    private readonly McpToolDefinition[] discovered;
    private readonly McpToolDefinition selector;
    private McpToolDefinition[] active;
    private static readonly HashSet<string> Core = new(StringComparer.Ordinal) {
        "topsolid_get_status", "topsolid_get_active_document", "topsolid_get_document_info", "topsolid_get_capabilities",
        "topsolid_list_projects", "topsolid_list_libraries", "topsolid_list_pdm_children", "topsolid_get_current_project", "topsolid_get_user_selection", "topsolid_get_object_model", "topsolid_find_pdm_documents", "topsolid_find_named_elements", "topsolid_resolve_pdm_documents" };

    public ToolExposure(McpToolDefinition[] discovered, string request = "")
    {
        this.discovered = discovered;
        if (discovered.Any(t => t.Name == SelectorName)) throw new InvalidOperationException("MCP tool name conflicts with Studio's schema selector.");
        selector = new McpToolDefinition { Name = SelectorName,
            Description = "Load schemas for up to 32 exact MCP tool names from the full catalog supplied in the system message. No separate capabilities query is needed. Selection does not execute tools or approve changes. Call the selected tools on the next request.",
            InputSchema = JObject.Parse("{\"type\":\"object\",\"properties\":{\"names\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":32,\"items\":{\"type\":\"string\"}}},\"required\":[\"names\"],\"additionalProperties\":false}"),
            Annotations = new JObject { ["readOnlyHint"] = true } };
        bool Has(string pattern) => Regex.IsMatch(request, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var sketch = Has(@"\b(sketch(?:es)?|skteches?|circles?|rectangles?|polylines?|contours?|profiles?|parabolas?|b-?splines?|arcs?|ellips[ei]s|ellipse|stars?|trees?|draw)\b|스케치|원형|포물선|스플라인|타원|별");
        var document = Has(@"\b(documents?|parts?|files?|assembly|assemblies|drafting|drawing|Top[A-Za-z0-9]+)\b|문서|부품|도면|조립");
        var pdm = Has(@"\b(project|projects|pdm|library|libraries|folder)\b|프로젝트|라이브러리");
        var create = Has(@"\b(create|make|add|new)\b|생성|만들");
        var preferred = new List<string>();
        if (Has(@"\bcheck[ -]?in\b|체크인")) preferred.AddRange(["topsolid_check_in_pdm_objects", "topsolid_get_document_creation_context"]);
        if (Has(@"\bsav(?:e|ing)\b|저장")) preferred.AddRange(["topsolid_save_documents", "topsolid_save_document"]);
        if (sketch && Has(@"\b(selected|selection)\b|선택")) preferred.Add("topsolid_get_user_selection");
        // A workflow crosses categories: creating a part is a PDM action, then
        // opening/saving it uses Documents and drawing it uses Sketch2D.
        if (document && create) preferred.AddRange(["topsolid_get_document_creation_context", "topsolid_create_part_document", "topsolid_create_document", "topsolid_open_document", "topsolid_save_document", "topsolid_list_document_types"]);
        if (Has(@"\btemplates?\b|템플릿") && !Has(@"\b(without|no)\s+(?:a\s+)?template\b|템플릿\s*없이"))
            preferred.AddRange(["topsolid_get_template_projects", "topsolid_list_pdm_children", "topsolid_get_pdm_object_info"]);
        if (pdm) preferred.AddRange(["topsolid_list_projects", "topsolid_list_libraries"]);
        if (pdm && create) preferred.AddRange(["topsolid_create_project", "topsolid_create_folder"]);
        if (sketch) preferred.AddRange(["topsolid_create_sketches2d", "topsolid_get_sketch2d_context", "topsolid_read_sketch2d_geometry",
            "topsolid_transform_sketch2d_points", "topsolid_create_sketch_profiles", "topsolid_create_circle2d", "topsolid_create_contour2d"]);
        if (Has(@"\b(extrude|extrusion|solid)\b|돌출")) preferred.AddRange(["topsolid_extrude_sections", "topsolid_create_extruded_rectangle", "topsolid_list_sketch2d_sections"]);
        if (Has(@"\b(revolve|revolution)\b|회전")) preferred.AddRange(["topsolid_revolve_sections", "topsolid_list_sketch2d_sections"]);
        if (document || sketch) preferred.AddRange(["topsolid_get_active_document", "topsolid_get_document_info", "topsolid_find_pdm_documents", "topsolid_find_named_elements"]);
        int Rank(McpToolDefinition t) {
            var category = (string?)t.Metadata["topsolid/category"];
            var preferredIndex = preferred.IndexOf(t.Name);
            if (preferredIndex >= 0) return 1000 - preferredIndex;
            if (t.Name == "topsolid_get_template_projects") return 0;
            if (t.Name is "topsolid_get_capabilities" or "topsolid_get_active_document" or "topsolid_get_document_info" or "topsolid_find_pdm_documents" or "topsolid_resolve_pdm_documents") return 8;
            if (sketch && t.Name is "topsolid_create_circle2d" or "topsolid_create_rectangle2d" or "topsolid_create_sketch_profiles" or "topsolid_read_sketch2d_geometry") return 7;
            if (sketch && category == "Sketch2D" || pdm && category == "Pdm") return 6;
            return Core.Contains(t.Name) ? 5 : IsBatch(t.Name) ? 3 : t.RequiresConfirmation ? 2 : 1;
        }
        active = discovered.OrderByDescending(Rank).ToArray();
        if (IsLimited) active = active.Take(InitialTools - 1).ToArray();
    }
    private bool IsLimited => discovered.Length > InitialTools;
    public McpToolDefinition[] Active => IsLimited ? active.Append(selector).ToArray() : active;
    public bool IsSelector(string name) => IsLimited && name == SelectorName;
    public string Catalog => !IsLimited ? "" :
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
        if (arguments.Count != 1 || arguments["names"] is not JArray names || names.Count is < 1 or > 32 || names.Any(v => v.Type != JTokenType.String))
            return McpToolResult.Error("Provide exactly names: an array of 1 to 32 exact discovered MCP tool names.");
        var requested = names.Select(v => (string)v!).Distinct(StringComparer.Ordinal).ToArray();
        if (requested.Any(name => !discovered.Any(t => t.Name == name))) return McpToolResult.Error("Unknown tool name. Use exact names from the full registered catalog in the system message; no tools were selected.");
        active = discovered.Where(t => Core.Contains(t.Name)).Concat(requested.Select(name => discovered.Single(t => t.Name == name)))
            .Concat(active).DistinctBy(t => t.Name).Take(MaximumTools - 1).ToArray();
        return new McpToolResult { Content = new JArray(new JObject { ["type"] = "text", ["text"] = new JObject {
            ["availableNextRequest"] = new JArray(requested), ["message"] = "Selected tool schemas are available on the next model request. No TopSolid operation was executed." }.ToString(Newtonsoft.Json.Formatting.None) }) };
    }
}
