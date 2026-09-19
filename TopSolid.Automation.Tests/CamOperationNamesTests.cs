using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class CamOperationNamesTests
{
    internal static Task Run()
    {
        var language = StudioStrings.CurrentLanguage;
        try
        {
            Check.Equal(72, CamOperationNames.Entries.Count, "Reviewed operation catalogue was not packaged");
            foreach (var entry in CamOperationNames.Entries.Values)
            {
                Check.True(Uri.TryCreate(entry.HelpUrl, UriKind.Absolute, out var source) && source.Scheme == "https" && source.Host == "help.topsolid.com" &&
                    source.AbsolutePath.StartsWith("/7.20/", StringComparison.Ordinal), "Operation has no official versioned help source");
                Check.True(!string.IsNullOrWhiteSpace(entry.OfficialHelpTitle) && !string.IsNullOrWhiteSpace(entry.KoreanName) &&
                    !entry.KoreanName.Contains("TopSolid."), "Missing reviewed label or official title");
                StudioStrings.Apply("ko");
                Check.Equal(entry.KoreanName, CamDisplay.Operation(entry.NativeType), "Korean operation lookup failed");
                StudioStrings.Apply("en");
                Check.Equal(entry.EnglishName, CamDisplay.Operation(entry.NativeType), "English operation lookup failed");
            }
            StudioStrings.Apply("ko");
            var examples = new Dictionary<string, string>
            {
                ["MillTurn.DB.EndMilling.NibblingOperation"] = "윤곽 황삭",
                ["MillTurn.FiveAxis.DB.GeodesicMachining.GeodesicMachiningOperation"] = "5축 정삭",
                ["MillTurn.FiveAxis.DB.MultiAxisRoughing.MultiAxisRoughingOperation"] = "다축 포켓 가공",
                ["MillTurn.Turn.DB.AngleGrooving.AngleGroovingOperation"] = "코너 릴리프",
                ["MillTurn.Form.DB.VerticalRoughing.VerticalRoughingOperation"] = "플런지 황삭",
                ["MillTurn.Form.DB.Roughing.RoughingOperation"] = "형상 황삭",
                ["MillTurn.Turn.DB.Roughing.RoughingOperation"] = "선삭 황삭",
                ["MillTurn.Form.DB.Finishing.FinishingOperation"] = "3D 정삭",
                ["MillTurn.Turn.DB.Finishing.FinishingOperation"] = "선삭 정삭",
                ["MillTurn.FiveAxis.DB.Contour.ContourOperation"] = "5축 윤곽 가공",
                ["MillTurn.Grinding.DB.Contour.ContourOperation"] = "윤곽 연삭",
                ["MillTurn.DB.PointToPoint.Operations.HoleOperation"] = "구멍 가공"
            };
            foreach (var (type, expected) in examples)
            {
                var full = "TopSolid.Cam.NC." + type;
                Check.Equal(expected, CamDisplay.Operation(full), "A CLR suffix was translated without respecting the official command/module");
                Check.Equal(expected + ".", CamDisplay.Text(full + "."), "Sentence punctuation broke exact class mapping");
            }
            Check.Equal("가공 작업", CamDisplay.Operation("TopSolid.Cam.NC.Future.DB.Finishing.FinishingOperation"), "Unknown module guessed a familiar finishing strategy");
            Check.Equal("가공 작업", CamDisplay.Operation("TopSolid.Cam.NC.MillTurn.Form.DB.Sweeping.Operation.SweepingOperationFake"), "Prefix match accepted an unverified class");

            // The view's label changes; the source row and actual target do not.
            var sources = new QuestionSources();
            var typeName = "TopSolid.Cam.NC.MillTurn.DB.EndMilling.NibblingOperation";
            var original = new JObject { ["operationName"] = "[8: 사용자 지정 이름]", ["operationType"] = typeName,
                ["operation"] = new JObject { ["element"] = new JObject { ["documentId"] = "verified-revision", ["id"] = 1042 } } };
            sources.Capture("read", CamSelectionRequest.Tool, new JObject(), new McpToolResult { StructuredContent = new JObject { ["items"] = new JArray(original.DeepClone()) } });
            var question = sources.Create(new JObject { ["question"] = "작업 선택", ["kind"] = "select", ["itemKind"] = "operation",
                ["sources"] = new JArray(new JObject { ["toolCallId"] = "read", ["path"] = "/items" }) });
            Check.Equal("[8: 사용자 지정 이름]", question.Choices[0].Label, "Type localization renamed the user's operation");
            Check.True(question.Choices[0].Detail.Contains("윤곽 황삭"), "Officially grounded name did not reach the card");
            var selected = question.Answer(selectedKeys: [question.Choices[0].Key]).Data["selected"]![0]!["value"];
            Check.True(JToken.DeepEquals(original, selected), "Localized operation card modified the executable receipt");
        }
        finally { StudioStrings.Apply(language); }
        return Task.CompletedTask;
    }
}
