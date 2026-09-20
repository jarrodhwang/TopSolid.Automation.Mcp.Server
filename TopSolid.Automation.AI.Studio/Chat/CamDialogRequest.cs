using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class CamDialogRequest
{
    internal static async Task<UserQuestion> ForPreview(JObject target, IMcpClient client, Action<ChatTrace> trace, CancellationToken token)
    {
        const string tool = "topsolid_get_cam_operation_info";
        if (target["operation"] is not JObject operation || !client.Tools.Any(t => t.Name == tool && !t.RequiresConfirmation))
            throw new InvalidOperationException(StudioStrings.Get("Cam.Unavailable"));
        var args = new JObject { ["element"] = operation.DeepClone() };
        var result = await client.CallToolAsync(tool, args, token);
        trace(new ChatTrace(result.IsError ? "Tool error" : "Tool result", ToolResultContext.Serialize(result), tool, args));
        var data = PdmInventory.Data(result);
        var resolved = data == null ? null : PreviewTarget.FromChoice(new JObject { ["value"] = data }, "operation");
        if (result.IsError || !JToken.DeepEquals(resolved?["operation"], operation))
            throw new InvalidOperationException(StudioStrings.Get("Cam.Unavailable"));
        var sources = new QuestionSources(); sources.Capture("cam-preview", tool, args, result);
        var page = sources.Create(new JObject { ["question"] = StudioStrings.Get("Cam.Operations"), ["kind"] = "select", ["itemKind"] = "operation",
            ["sources"] = new JArray(new JObject { ["toolCallId"] = "cam-preview", ["path"] = "" }) });
        var question = UserQuestion.Browse([page], 1, false, null, title: StudioStrings.Get("Cam.Operations"));
        question.InitialInspectionKey = question.Choices.Single().Key;
        return question;
    }
}
