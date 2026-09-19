using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

// These narrow rules apply to the current typed request, never to attachments,
// old conversation text or names returned by TopSolid.
internal sealed class CamWorkflow
{
    internal const string ListTool = "topsolid_list_cam_parameters";
    private readonly bool chooseFeed;
    private readonly bool korean;
    private JObject? selectedParameter;
    private JObject? selectedScope;
    private bool valueAnswered;
    private readonly List<JObject> rows = [];
    private JObject? scope;
    private string operationName = "";
    private int? total;
    private int? nextOffset;
    private string? incomplete;

    internal CamWorkflow(string request, string language)
    {
        bool Has(string pattern) => Regex.IsMatch(request, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        AllCuttingConditions = language is "auto" or "en" or "ko" && Has(@"\ball\b|모든|전체") && Has(@"cutting[ -]?conditions?|절삭\s*조건") &&
            Has(@"\b(show|list|inspect|display|read)\b|보여|조회|목록") &&
            !Has(@"\b(change|edit|modify|set|create|delete|remove|save|export|strategy|geometry|library|documents?|abacus)\b|변경|수정|생성|삭제|저장|전략|라이브러리|문서");
        chooseFeed = Has(@"\b(select|choose|pick)\b|선택|고르") &&
            Has(@"\b(?:editable\s+)?feed(?:[ -]?rate)?\s+parameters?\b|(?:이송|피드).{0,16}(?:매개변수|파라미터)") &&
            !Has(@"\b(do not|don't|without)\s+(?:asking|selecting|choosing)|선택\s*없이");
        korean = language == "ko" || language == "auto" && Has("[가-힣]");
    }

    internal bool AllCuttingConditions { get; }
    internal bool HasInventory => scope != null;
    internal string Instructions =>
        (AllCuttingConditions ? "This request needs every operation cutting-condition parameter: call topsolid_list_cam_parameters with category=CuttingConditions and offset=0. Studio follows continuation pages and displays every returned row. " : "") +
        (chooseFeed ? "The user must choose the editable feed parameter as well as the operation. Query list_cam_parameters with nameContains=Feed; select from those receipts using itemKind=camParameter and sources.editableOnly=true. Do not choose the parameter yourself. After that selection ask for the new value, with the verified input unit and no invented numeric bounds; then prepare the exact selected parameter. " : "");

    internal string? Validate(AiToolCall call)
    {
        if (AllCuttingConditions && call.Name == ListTool &&
            (!string.Equals((string?)call.Arguments["category"], "CuttingConditions", StringComparison.OrdinalIgnoreCase) ||
             !string.IsNullOrWhiteSpace((string?)call.Arguments["nameContains"])))
            return "Read all cutting conditions with category=CuttingConditions and without nameContains. Do not substitute a page of unrelated operation parameters.";
        if (!chooseFeed) return null;
        if (call.Name == QuestionSources.ToolName && (string?)call.Arguments["itemKind"] == "camParameter" &&
            call.Arguments["sources"] is JArray sources && sources.Any(source => (bool?)source["editableOnly"] != true))
            return "Use sources.editableOnly=true so the parameter picker only offers verified editable parameters.";
        if (call.Name == QuestionSources.ToolName && (string?)call.Arguments["kind"] is "decimal" or "integer")
        {
            if (selectedParameter == null) return "First let the user SELECT the editable feed parameter from current list_cam_parameters receipts. Use itemKind=camParameter and sources.editableOnly=true. A numeric question does not select a parameter.";
            if (call.Arguments["minimum"] != null || call.Arguments["maximum"] != null)
                return "The CAM API did not supply numeric limits. Omit minimum and maximum; do not invent machining limits.";
        }
        if (call.Name == "topsolid_set_cam_parameter_value")
        {
            if (selectedParameter == null || !valueAnswered) return "The user must select an editable feed parameter and answer its value question before a change can be prepared.";
            if ((string?)call.Arguments["name"] != (string?)selectedParameter["name"] ||
                !JToken.DeepEquals(call.Arguments["element"], selectedScope?["element"]) ||
                !JToken.DeepEquals(call.Arguments["documentId"], selectedScope?["element"]?["documentId"]))
                return "The change does not match the user's selected parameter and operation. Keep the exact selected name and sourceArguments.element.";
        }
        return null;
    }

    internal void Answered(QuestionAnswer answer)
    {
        if (!chooseFeed) return;
        if (answer.Data["selected"] is JArray selected)
        {
            if (selected.Count != 1 || selected[0] is not JObject item) { selectedParameter = null; selectedScope = null; valueAnswered = false; return; }
            if ((string?)item["sourceTool"] is ListTool or "topsolid_get_cam_parameter_value" && item["value"] is JObject row &&
                (bool?)row["editSupported"] == true && (bool?)row["readOnly"] == false &&
                ((string?)row["name"] ?? "").Contains("feed", StringComparison.OrdinalIgnoreCase) && item["sourceArguments"]?["element"] is JObject)
            {
                selectedParameter = (JObject)row.DeepClone(); selectedScope = (JObject)item["sourceArguments"]!.DeepClone(); valueAnswered = false;
            }
            else if ((string?)item["sourceTool"] is "topsolid_list_cam_operation_summaries" or "topsolid_list_cam_operations")
            { selectedParameter = null; selectedScope = null; valueAnswered = false; }
        }
        else if (selectedParameter != null && (string?)answer.Data["kind"] is "decimal" or "integer") valueAnswered = true;
    }

    internal void Capture(string tool, JObject arguments, McpToolResult result)
    {
        if (!AllCuttingConditions || tool != ListTool) return;
        var requestedScope = (JObject)arguments.DeepClone(); requestedScope.Remove("offset"); requestedScope.Remove("limit");
        if (!string.Equals((string?)requestedScope["category"], "CuttingConditions", StringComparison.OrdinalIgnoreCase)) return;
        var offset = (int?)arguments["offset"] ?? 0;
        if (scope == null) { scope = requestedScope; nextOffset = 0; }
        if (!JToken.DeepEquals(scope, requestedScope)) { incomplete = "Operation changed during pagination."; nextOffset = null; return; }
        if (ToolResultContext.Serialize(result).Length > 64000) { incomplete = "A page exceeded the result limit."; nextOffset = null; return; }
        var data = PdmInventory.Data(result);
        if (result.IsError || data?["items"] is not JArray items || data["total"]?.Type != JTokenType.Integer ||
            data["offset"]?.Type != JTokenType.Integer || data["hasMore"]?.Type != JTokenType.Boolean || (int?)data["offset"] != offset || offset != nextOffset ||
            items.Any(item => item is not JObject))
        { incomplete = "A page failed or returned invalid pagination metadata."; nextOffset = null; return; }
        var pageTotal = (int)data["total"]!;
        var more = (bool)data["hasMore"]!;
        var next = offset + items.Count;
        if (pageTotal < 0 || total.HasValue && total != pageTotal || next > pageTotal || more != (next < pageTotal) ||
            more && (items.Count == 0 || data["nextOffset"]?.Type != JTokenType.Integer || (int?)data["nextOffset"] != next) || rows.Count + items.Count > 2000 ||
            rows.Concat(items.OfType<JObject>()).GroupBy(row => (string?)row["name"], StringComparer.Ordinal).Any(group => group.Key == null || group.Count() > 1))
        { incomplete = "The list changed or its continuation did not advance. Refresh before relying on the full list."; nextOffset = null; return; }
        total = pageTotal;
        operationName = (string?)data["operationName"] ?? operationName;
        rows.AddRange(items.OfType<JObject>().Select(row => (JObject)row.DeepClone()));
        nextOffset = more ? next : null;
    }

    internal JObject? NextPage()
    {
        if (scope == null || nextOffset == null || incomplete != null) return null;
        var arguments = (JObject)scope.DeepClone(); arguments["offset"] = nextOffset; arguments["limit"] = 100;
        return arguments;
    }

    internal string Render()
    {
        string L(string en, string ko) => korean ? ko : en;
        string Cell(JToken? value) => (value?.Type == JTokenType.Null ? "" : value?.ToString() ?? "")
            .Replace("\r", " ").Replace("\n", " ");
        var result = new StringBuilder();
        result.AppendLine($"{Cell(new JValue(operationName))} — {L("cutting conditions", "절삭 조건")} ({rows.Count.ToString(CultureInfo.InvariantCulture)}/{total?.ToString(CultureInfo.InvariantCulture) ?? "?"})");
        if (incomplete != null || nextOffset != null)
            result.AppendLine(L("Incomplete list. Refresh the remaining pages before relying on it.", "일부 목록입니다. 남은 페이지를 다시 조회하세요."));
        result.AppendLine();
        var index = 0;
        foreach (var row in rows)
        {
            var choices = row["allowedValues"] is JArray values ? string.Join(", ", values.Select(v => v["label"] != null || v["name"] != null
                ? Cell(v["value"]) + " = " + Cell(v["label"] ?? v["name"]) : Cell(v["value"]))) : L("Not supplied", "제공되지 않음");
            var edit = (bool?)row["isError"] == true ? L("Read failed", "조회 실패") : (bool?)row["editSupported"] == true ? L("Editable", "편집 가능") :
                (bool?)row["readOnly"] == true ? L("Read-only", "읽기 전용") : L("No supported edit", "편집 지원 없음");
            if (row["metadataErrors"] is JObject { Count: > 0 }) edit += L("; metadata incomplete", "; 메타데이터 일부 누락");
            // ChatTranscript is plain WPF text, so use readable groups instead
            // of markdown table delimiters that would wrap as raw punctuation.
            result.AppendLine($"{++index}. {Cell(row["displayName"] ?? row["localizedName"] ?? row["name"])} — {Cell(row["displayValue"])}");
            result.AppendLine($"   {L("Unit type", "단위 형식")}: {Cell(row["unitType"] ?? row["valueType"])} · {edit}");
            result.AppendLine($"   {L("Allowed choices", "허용 선택")}: {choices}");
        }
        result.AppendLine();
        result.Append(L("Numeric limits are not supplied by the CAM API. No changes were made.", "CAM API에서 숫자 범위를 제공하지 않습니다. 변경하지 않았습니다."));
        return result.ToString();
    }
}
