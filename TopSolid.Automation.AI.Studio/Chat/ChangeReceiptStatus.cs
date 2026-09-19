using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

internal sealed class ChangeReceiptStatus
{
    private readonly HashSet<string> unsaved = new(StringComparer.Ordinal);
    private readonly HashSet<string> camRecalculation = new(StringComparer.Ordinal);
    private bool ncNotGenerated;

    internal void Capture(McpToolResult result)
    {
        if (result.IsError || PdmInventory.Data(result) is not { } data) return;
        Record(data);
        if (data["items"] is JArray items) foreach (var item in items.OfType<JObject>()) Record(item);
    }

    private void Record(JObject data)
    {
        if ((bool?)data["isError"] == true || data["documentId"]?.Type != JTokenType.String) return;
        var id = (string)data["documentId"]!;
        var original = (string?)data["originalDocumentId"];
        if (original != null && original != id) {
            if (unsaved.Remove(original)) unsaved.Add(id);
            if (camRecalculation.Remove(original)) camRecalculation.Add(id);
        }
        if (data["saved"]?.Type == JTokenType.Boolean) {
            if ((bool)data["saved"]!) unsaved.Remove(id); else unsaved.Add(id);
        }
        if ((bool?)data["recalculationMayBeRequired"] == true) camRecalculation.Add(id);
        if ((bool?)data["upToDate"] == true) camRecalculation.Remove(id);
        if ((bool?)data["ncGenerated"] == false) ncNotGenerated = true;
    }

    internal string Append(string answer, string language)
    {
        var ko = language == "ko" || language == "auto" && Regex.IsMatch(answer, "[가-힣]");
        var labels = language switch {
            "ja" => new[] { "変更はまだ保存されていません。", "加工操作の再計算が必要な場合があります。", "NCコードは生成されていません。" },
            "zh" => new[] { "更改尚未保存。", "加工操作可能需要重新计算。", "未生成NC代码。" },
            "fr" => new[] { "Les modifications ne sont pas encore enregistrées.", "L’opération d’usinage peut nécessiter un recalcul.", "Aucun code CN n’a été généré." },
            "de" => new[] { "Die Änderungen sind noch nicht gespeichert.", "Der Bearbeitungsvorgang muss möglicherweise neu berechnet werden.", "Es wurde kein NC-Code erzeugt." },
            "es" => new[] { "Los cambios aún no se han guardado.", "La operación de mecanizado puede requerir un nuevo cálculo.", "No se ha generado código NC." },
            _ when ko => new[] { "변경 사항은 아직 저장되지 않았습니다.", "가공 작업을 다시 계산해야 할 수 있습니다.", "NC 코드는 생성하지 않았습니다." },
            _ => new[] { "Changes are not saved yet.", "The machining operation may need recalculation.", "NC code was not generated." }
        };
        var notes = new List<string>();
        if (unsaved.Count > 0) notes.Add(labels[0]);
        if (camRecalculation.Count > 0) notes.Add(labels[1]);
        if (ncNotGenerated) notes.Add(labels[2]);
        return notes.Count == 0 ? answer : answer.TrimEnd() + "\n\n" + string.Join(" ", notes);
    }
}
