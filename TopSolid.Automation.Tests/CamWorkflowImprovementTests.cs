using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class CamWorkflowImprovementTests
{
    internal static JObject Receipt(string name="Safety@Strategy", string type="Real") => new()
    {
        ["sourceTool"]="topsolid_list_cam_parameters",["sourceArguments"]=JObject.Parse("{element:{documentId:'cam-rev',id:12}}"),
        ["value"]=new JObject {["name"]=name,["displayName"]=name,["valueType"]=type,["editSupported"]=true,["readOnly"]=false,
            ["realValueSI"]=.002,["unitType"]="Length",["displayValue"]="2 mm"}
    };
    internal static async Task Run()
    {
        var draft=new CamParameterDraft(Receipt());var args=draft.Arguments("3",null);
        Check.Equal(.003,(double)args["realValueSI"]!,"Millimetres did not become SI metres");
        Check.Equal("Length",(string?)args["unitType"],"Native unit type was lost");
        Check.True(!draft.Changed(draft.Arguments(draft.InitialText,null)),"Untouched real input became a write");
        Check.Throws<ArgumentException>(() => draft.Arguments("NaN",null));
        Check.Throws<ArgumentException>(() => draft.ValidateValue(new JValue("3")));
        var enumReceipt = Receipt("Mode", "Integer");
        enumReceipt["value"]!["integerValue"] = 0;
        enumReceipt["value"]!["allowedValues"] = new JArray(new JObject { ["value"] = 0 }, new JObject { ["value"] = 2 });
        var nativeEnum = new CamParameterDraft(enumReceipt);
        Check.Throws<ArgumentException>(() => nativeEnum.Arguments("", new JValue(1)));
        var choices=new[]{new QuestionChoice("a","1","","operation","1",ToolGroupKey:"tool-1"),new QuestionChoice("b","2","","operation","2",ToolGroupKey:"tool-1"),new QuestionChoice("c","3","","operation","3",ToolGroupKey:"tool-2"),new QuestionChoice("d","4","","operation","4",ToolGroupKey:"tool-1")};
        var runs=OperationToolGroups.Runs(choices);
        Check.True(runs["a"]==runs["b"] && runs["b"]!=runs["d"] && runs["c"]!=runs["d"],"Grouping reordered a returning tool");
        var q=new UserQuestion("Operations","select","operation",true,"",null,null,choices,choices.ToDictionary(c=>c.Key,c=>new JObject {["value"]=c.Label}));
        Check.Equal("1, 4",q.Answer(selectedKeys:["d","a"]).Summary,"Click order overrode native operation order");
        foreach(var approve in new[]{true,false})
        {
            var client=new Client();using var model=new FakeAiProvider();var session=new ChatSession(model,client);
            var selection=new McpToolResult {StructuredContent=new JObject {["status"]="answered",["kind"]="select",["selected"]=new JArray(Receipt(),Receipt("Other@Strategy"))}};
            session.RestoreConversation([[new AiMessage {Role="user",Content="Select parameters"},new AiMessage {Role="tool",ToolName=QuestionSources.ToolName,Content=ToolResultContext.Serialize(selection)}]]);
            var edits=0;session.EditCamParametersAsync=(rows,_)=>{edits++;return Task.FromResult<IReadOnlyList<JObject>?>(rows.Select(r=>new CamParameterDraft(r).Arguments("3",null)).ToArray());};
            session.ConfirmChangeAsync=(_,_)=>Task.FromResult(approve);
            await session.SendAsync("선택한 파라미터들을 수정하는 다이로그 띄워줘",CancellationToken.None);
            Check.Equal(0,model.CompletionCount,"Local parameter editor called the AI model");Check.Equal(1,edits,"Selected parameters did not share one editor");
            Check.Equal(approve?2:0,client.Writes.Count,"Cancelled editing wrote CAM data");
            if(approve) Check.Equal("cam-rev-next",(string?)client.Writes[1]["documentId"],"Later edit used a stale revision");
        }
        await SelectionGoesToEditor();
    }

    private static async Task SelectionGoesToEditor()
    {
        var client = new Client(); var editors = 0;
        using var model = new FakeAiProvider { Reply = (round,_,_) => Task.FromResult(round switch
        {
            1 => FakeAiProvider.ToolReply(new AiToolCall { Id="parameters", Name="topsolid_list_cam_parameters", Arguments=(JObject)Receipt()["sourceArguments"]! }),
            2 => FakeAiProvider.ToolReply(new AiToolCall { Id="selection", Name=QuestionSources.ToolName,
                Arguments=JObject.Parse("{question:'Select CAM parameters',kind:'select',itemKind:'camParameter',multiple:true,sources:[{toolCallId:'parameters',path:'/items'}]}") },
                new AiToolCall { Id="premature", Name=CamParameterEditRequest.WriteTool, Arguments=new JObject() }),
            _ => throw new InvalidOperationException("Selection unnecessarily returned to the model")
        }) };
        var session = new ChatSession(model,client)
        {
            AskUserAsync=(q,_) => Task.FromResult<QuestionAnswer?>(q.Answer(selectedKeys:q.Choices.Select(c=>c.Key))),
            EditCamParametersAsync=(rows,_) => { editors++; Check.Equal(2,rows.Count,"Next editor lost selected parameters"); return Task.FromResult<IReadOnlyList<JObject>?>([]); },
            ConfirmChangeAsync=(_,_) => throw new InvalidOperationException("Unchanged values required confirmation")
        };
        await session.SendAsync("CAM 파라미터 수정 대화상자를 열어줘",CancellationToken.None);
        Check.Equal(2,model.CompletionCount,"Local editor required another model round");
        Check.Equal(1,editors,"Selection did not open the next editor");
        Check.Equal(0,client.Writes.Count,"Selection executed a premature change");
        var history=session.GetConversationSnapshot().Single();
        foreach(var call in history.SelectMany(m=>m.ToolCalls))
            Check.Equal(1,history.Count(m=>m.Role=="tool" && m.ToolCallId==call.Id),"Local continuation left an unpaired tool call");
    }
    internal static async Task Context(string path)
    {
        var scenes=await CamContextPreview.ReadAsync(path,CancellationToken.None);
        Check.True(scenes.Work.Triangles>0 && scenes.Machine.Triangles>scenes.Work.Triangles,"Native machine geometry was omitted");
        Check.True(scenes.Machine.Bounds.SizeX>scenes.Work.Bounds.SizeX,"Machine bounds did not include the machine");
        Console.WriteLine($"CAM context: work {scenes.Work.Triangles} triangles, machine+work {scenes.Machine.Triangles}; bounds {scenes.Work.Bounds} / {scenes.Machine.Bounds}");
    }

    internal static async Task LiveContext(string server)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(55));
        await using var client = new StdioMcpClient();
        await client.ConnectAsync(server, timeout.Token);
        async Task<JObject> ReadDocument()
        {
            var result = await client.CallToolAsync("topsolid_get_document_info", new JObject(), timeout.Token);
            Check.True(!result.IsError, "Cannot read the active CAM document");
            return JObject.Parse((string)result.Content[0]["text"]!);
        }
        var before = await ReadDocument();
        var target = new JObject { ["documentId"] = before["document"]!["documentId"]!.DeepClone(), ["camContext"] = true };
        using var payload = await PreviewFileTransfer.DownloadAsync(client, target, timeout.Token);
        Check.Equal("ready", (string?)payload.Metadata["status"], "Native CAM export unavailable");
        Check.Equal(true, (bool?)payload.Metadata["camContext"], "Server did not use organized CAM geometry");
        var scenes = await CamContextPreview.ReadAsync(payload.FilePath!, timeout.Token);
        Check.True(scenes.Machine.Triangles > scenes.Work.Triangles && scenes.Work.Triangles > 0, "Live context omitted machine/work geometry");
        var after = await ReadDocument();
        Check.True(JToken.DeepEquals(before["document"], after["document"]), "CAM export changed the active document or its dirty state");
        var directory = Path.GetFullPath("artifacts/cam-context"); Directory.CreateDirectory(directory);
        var report = new JObject { ["documentUnchanged"] = true, ["workTriangles"] = scenes.Work.Triangles,
            ["machineAndWorkTriangles"] = scenes.Machine.Triangles, ["metadata"] = payload.Metadata.DeepClone() };
        File.WriteAllText(Path.Combine(directory, "live-context-rpc.json"), report.ToString());
        Console.WriteLine(report);
    }
    private sealed class Client:IConfirmableMcpClient
    {
        public bool IsConnected=>true;
        public List<JObject> Writes {get;}=[];
        public IReadOnlyList<McpToolDefinition> Tools {get;}=[new(){Name="topsolid_list_cam_parameters",Annotations=new JObject {["readOnlyHint"]=true}},new(){Name=CamParameterEditRequest.ReadTool,Annotations=new JObject {["readOnlyHint"]=true}},new(){Name=CamParameterEditRequest.WriteTool,Annotations=new JObject {["readOnlyHint"]=false}}];
        public Task<McpToolResult> CallToolAsync(string name,JObject args,CancellationToken token)=>Task.FromResult(new McpToolResult {StructuredContent=name=="topsolid_list_cam_parameters"
            ? new JObject { ["items"]=new JArray(Receipt()["value"]!,Receipt("Other@Strategy")["value"]!) }
            : (JObject)Receipt((string)args["name"]!)["value"]!});
        public Task<JObject> PrepareToolAsync(string name,JObject args,CancellationToken token)=>Task.FromResult(new JObject {["toolName"]=name,["arguments"]=args.DeepClone(),["target"]=new JObject {["name"]="Operation"},["confirmationToken"]="fixture"});
        public Task<McpToolResult> CallConfirmedToolAsync(string name,JObject args,string confirmationToken,CancellationToken token)
        {Writes.Add((JObject)args.DeepClone());return Task.FromResult(new McpToolResult {StructuredContent=new JObject {["documentId"]="cam-rev-next",["originalDocumentId"]=args["documentId"]!.DeepClone(),["saved"]=false}});}
    }
}
