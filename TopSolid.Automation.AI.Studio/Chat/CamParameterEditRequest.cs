using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class CamParameterEditRequest
{
    internal const string ReadTool = "topsolid_get_cam_parameter_value", WriteTool = "topsolid_set_cam_parameter_value";
    internal static bool Matches(string text) => Regex.IsMatch(text, @"선택|\bselected\b", RegexOptions.IgnoreCase) && WantsEdit(text);
    internal static bool WantsEdit(string text) => Regex.IsMatch(text, @"파라미터|매개변수|\bparameters?\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(text, @"수정|편집|\b(?:edit|modify|change)\b", RegexOptions.IgnoreCase) &&
        !Regex.IsMatch(text, @"하지\s*마|말고|않|\b(?:don't|do not|without|explain|how)\b", RegexOptions.IgnoreCase);

    internal static async Task<IReadOnlyList<AiMessage>?> Run(string text, IReadOnlyList<AiMessage>? previous, IMcpClient mcp,
        Func<IReadOnlyList<JObject>, CancellationToken, Task<IReadOnlyList<JObject>?>>? edit,
        Func<JObject, CancellationToken, Task<bool>>? confirm, Action<ChatTrace> trace, CancellationToken token)
    {
        if (!Matches(text)) return null;
        var turn = new List<AiMessage> { new() { Role = "user", Content = text } }; var completed = 0;
        IReadOnlyList<AiMessage> Finish(string key, params object[] args) { turn.Add(new AiMessage { Role = "assistant", Content = StudioStrings.Get(key, args) }); return turn; }
        if (edit == null || confirm == null || mcp is not IConfirmableMcpClient client || !mcp.IsConnected ||
            !mcp.Tools.Any(t => t.Name == ReadTool && !t.RequiresConfirmation) || !mcp.Tools.Any(t => t.Name == WriteTool && t.RequiresConfirmation)) return Finish("Cam.EditUnavailable");
        var selection = previous?.LastOrDefault(m => m.Role == "tool" && m.ToolName == QuestionSources.ToolName);
        JObject? answer = null;
        try { if (selection != null) answer = JObject.Parse(selection.Content)["structuredContent"] as JObject; } catch (Newtonsoft.Json.JsonException) { }
        if ((string?)answer?["status"] != "answered" || answer["selected"] is not JArray { Count: > 0 and <= 100 } selected ||
            selected.Any(s => s is not JObject || (string?)s["sourceTool"] != "topsolid_list_cam_parameters")) return Finish("Cam.EditUnavailable");
        try
        {
            var refreshed = new List<JObject>();
            foreach (var choice in selected.OfType<JObject>())
            {
                token.ThrowIfCancellationRequested();
                var name = (string?)choice["value"]?["name"]; var element = choice["sourceArguments"]?["element"] as JObject;
                if (name == null || element == null) return Finish("Cam.EditUnavailable");
                var args = new JObject { ["element"] = element.DeepClone(), ["name"] = name };
                var result = await Read(args);
                var row = PdmInventory.Data(result);
                if (result.IsError || row == null || (string?)row["name"] != name) return Finish("Cam.EditUnavailable");
                var receipt = (JObject)choice.DeepClone(); receipt["value"] = row.DeepClone();
                try { _ = new CamParameterDraft(receipt); } catch (ArgumentException) { return Finish("Cam.EditUnavailable"); }
                refreshed.Add(receipt);
            }
            var changes = await edit(refreshed.Select(r => (JObject)r.DeepClone()).ToArray(), token);
            if (changes == null) return Finish("Cam.Cancelled");
            // Only exact selected operations/parameters and typed fields may leave this local editor.
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var change in changes)
            {
                var draft = refreshed.Select(r => new CamParameterDraft(r)).SingleOrDefault(d =>
                    JToken.DeepEquals(d.Receipt["sourceArguments"]!["element"], change["element"]) && JToken.DeepEquals(d.Row["name"], change["name"]));
                if (draft == null || change.Properties().Any(p => p.Name is not ("documentId" or "element" or "name" or "valueType" or "unitType") && p.Name != draft.Field) ||
                    (string?)change["valueType"] != draft.Type || !JToken.DeepEquals(change["documentId"], change["element"]?["documentId"]) ||
                    !JToken.DeepEquals(change["unitType"], draft.Type == "Real" ? draft.Row["unitType"] : null) ||
                    !unique.Add(change["element"]!.ToString() + change["name"]!.ToString())) throw new ArgumentException("Invalid local CAM edit.");
                draft.ValidateValue(change[draft.Field]);
            }
            var revisions = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (var original in changes)
            {
                token.ThrowIfCancellationRequested(); var args = (JObject)original.DeepClone();
                var document = (string)args["documentId"]!;
                if (revisions.TryGetValue(document, out var current)) { args["documentId"] = current; args["element"]!["documentId"] = current; }
                var proposal = await client.PrepareToolAsync(WriteTool, args, token);
                if ((string?)proposal["toolName"] != WriteTool || !JToken.DeepEquals(args, proposal["arguments"]) || proposal["target"] is not JObject ||
                    string.IsNullOrWhiteSpace((string?)proposal["confirmationToken"])) throw new InvalidOperationException("Invalid CAM change preview.");
                var visible = (JObject)proposal.DeepClone(); visible.Remove("confirmationToken");
                if (!await confirm(visible, token)) return Finish("Cam.EditStopped", completed);
                token.ThrowIfCancellationRequested();
                var result = await Execute(WriteTool,args,() => client.CallConfirmedToolAsync(WriteTool,args,(string)proposal["confirmationToken"]!,token));
                if (result.IsError) return Finish("Cam.EditStopped",completed);
                completed++;
                var data = PdmInventory.Data(result);
                if (data?["documentId"]?.Type == JTokenType.String && (string?)data["originalDocumentId"] == (string?)args["documentId"])
                    revisions[document] = (string)data["documentId"]!;
                else if (completed < changes.Count) return Finish("Cam.EditStopped",completed);
            }
            return Finish("Cam.EditComplete",completed);
        }
        catch (OperationCanceledException) { return Finish("Cam.EditStopped",completed); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.IO.IOException or TimeoutException)
        { trace(new ChatTrace("Tool error", error.Message)); return Finish("Cam.EditStopped",completed); }

        string Start(string name,JObject args)
        {
            var id="cam-edit-"+Guid.NewGuid().ToString("N");
            turn.Add(new AiMessage {Role="assistant",ToolCalls=[new AiToolCall {Id=id,Name=name,Arguments=(JObject)args.DeepClone(),ClientInitiated=true}]});
            trace(new ChatTrace("Tool call",name,name,args)); return id;
        }
        void End(string id,string name,JObject args,McpToolResult result)
        {
            var content=ToolResultContext.Serialize(result);
            turn.Add(new AiMessage {Role="tool",ToolCallId=id,ToolName=name,Content=content});
            trace(new ChatTrace(result.IsError?"Tool error":name==WriteTool?"CAD change":"Tool result",content,name,args));
        }
        async Task<McpToolResult> Read(JObject args)
            => await Execute(ReadTool,args,() => client.CallToolAsync(ReadTool,args,token));
        async Task<McpToolResult> Execute(string name,JObject args,Func<Task<McpToolResult>> call)
        {
            var id=Start(name,args);
            try { var result=await call(); End(id,name,args,result); return result; }
            catch (Exception error)
            { End(id,name,args,McpToolResult.Error(error is OperationCanceledException ? "Request cancelled; no automatic retry." : error.Message)); throw; }
        }
    }
}
