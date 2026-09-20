using System.IO;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class CamColorTests
{
    internal static async Task Run()
    {
        var directory=Path.Combine(Path.GetTempPath(),"Studio-color-tests-"+Guid.NewGuid().ToString("N"));
        var store=new SettingsStore(directory);
        try
        {
            Check.True(!store.Load().ContextOptions.Cad&&!store.Load().ContextOptions.Cam,"First launch must leave both modes off");
            foreach(var cad in new[]{false,true}) foreach(var cam in new[]{false,true})
            {
                store.SaveContextOptions(new(){Cad=cad,Cam=cam}); var restored=store.Load();
                Check.True(restored.ContextOptions.Cad==cad && restored.ContextOptions.Cam==cam,"Context did not survive restart");
                var instructions=ModelInstructions.Build([],"",false,context:restored.ContextOptions);
                Check.Equal(cad,instructions.Contains("CAD context enabled"),"CAD instructions");Check.Equal(cam,instructions.Contains("CAM context enabled"),"CAM instructions");
            }
            var palette=CamColorStandard.Starter();palette.Validate();
            Check.Equal(8,palette.Roles.Count,"Starter palette role count");Check.Equal("#0066FF",palette.Roles.Single(r=>r.Key=="pocket").Hex,"Pocket RGB");
            palette.Roles[0].Hex="#010203";Check.Throws<ArgumentException>(palette.Validate);
            palette.Id="custom-fixture";palette.Validate();store.SaveCamColorStandard(palette);
            Check.Equal("#010203",store.Load().CamColorStandard.Roles[0].Hex,"Custom palette persistence");
            store.SaveContextOptions(new(){Cad=true});Check.Equal("custom-fixture",store.Load().CamColorStandard.Id,"Toggle overwrote palette");
            palette.Roles[1].Hex=palette.Roles[0].Hex;Check.Throws<ArgumentException>(palette.Validate);

            var context=new StudioContextOptions {Cam=true};
            Check.True(CamColorPreparation.Matches("Analyze this part and prepare CAM colors",new()),"Preparation routing");
            Check.True(!CamColorPreparation.Matches("Explain how CAM color preparation works",context),"Explanation routed to mutation workflow");
            Check.True(!CamColorPreparation.Matches("Don't prepare CAM colors",context),"Negative request routed to preparation");
            var plan=Fixture();var aliases=new Dictionary<string,JObject>{{"g1",(JObject)plan.Geometry[0]!}};
            CamColorPreparation.AddProposals(plan,Proposal(),aliases);
            Check.Equal("native-target",plan.Assignments.Single().TargetKey,"AI alias replaced exact target");
            Check.Throws<ArgumentException>(()=>CamColorPreparation.AddProposals(plan,Proposal(),aliases));
            var bad=Proposal();bad["assignments"]![0]!["targetKey"]="invented";
            Check.Throws<ArgumentException>(()=>CamColorPreparation.AddProposals(Fixture(),bad,aliases));
            var arguments=plan.ToArguments();Check.True(JToken.DeepEquals(arguments["targets"]![0]!["target"],plan.Geometry[0]!["target"]),"Native identity changed");
            plan.Geometry[0]!["colorSupported"]=false;Check.Throws<ArgumentException>(()=>plan.ToArguments());
            var uncertain=Fixture();
            CamColorPreparation.AddProposals(uncertain,JObject.Parse("{assignments:[],unassigned:[{targetKey:'g1',reason:'A profile alone does not establish a pocket.'}]}"),
                new Dictionary<string,JObject>{{"g1",(JObject)uncertain.Geometry[0]!}});
            Check.Equal(0,uncertain.Assignments.Count,"Uncertain geometry acquired a role");
            Check.True(((string?)uncertain.Geometry[0]!["proposalReason"])!.Contains("pocket"),"Uncertainty reason was lost");
            foreach(var approved in new[]{true,false}) foreach(var reviewed in new[]{true,false})
            {
                var client=new Client();using var model=new FakeAiProvider {Reply=(_,_,_)=>Task.FromResult(new AiReply {ToolCalls=[new(){Id="proposal",Name=CamColorPreparation.Propose,Arguments=Proposal()}]})};
                var session=new ChatSession(model,client) {ContextOptions=new(){Cam=true},ReviewCamColorsAsync=(p,_)=>Task.FromResult(reviewed?p:null),
                    ConfirmChangeAsync=(p,_)=> {Check.True(p["confirmationToken"]==null,"Ticket leaked to UI");return Task.FromResult(approved);} };
                await session.SendAsync("Analyze this part and prepare CAM colors",CancellationToken.None);
                Check.Equal(approved&&reviewed?1:0,client.Writes,"Color review/approval bypass");
                Check.Equal(reviewed?1:0,client.Prepares,"Preparation before review");
                Check.True(model.SuppliedTools.All(names=>names.SequenceEqual(new[]{CamColorPreparation.Propose})),"AI received native writes during classification");
                session.Clear();Check.True(session.ContextOptions.Cam,"Clearing chat cleared context");
            }
            var incomplete=new Client {Incomplete=true};using var unused=new FakeAiProvider();
            await new ChatSession(unused,incomplete) {ReviewCamColorsAsync=(p,_)=>throw new Exception("Incomplete data opened review"),ConfirmChangeAsync=(_,_)=>Task.FromResult(true)}
                .SendAsync("Prepare CAM colors for this part",CancellationToken.None);
            Check.Equal(0,unused.CompletionCount,"Incomplete geometry reached AI");
            var proposal=new JObject { ["toolName"]=CamColorPreparation.Apply,["arguments"]=arguments,["target"]=new JObject{["documentId"]="rev"} };
            Check.True(PermissionPolicy.Evaluate(PermissionMode.AskForApproval,proposal).RequiresApproval,"Ask mode bypassed");
            Check.True(!PermissionPolicy.Evaluate(PermissionMode.FullAccess,proposal).RequiresApproval,"Full access no longer applies to reviewed colors");
            var catalog=Enumerable.Range(0,40).Select(i=>new McpToolDefinition {Name="other_"+i}).Concat(new[]{
                new McpToolDefinition{Name="topsolid_list_cam_operation_summaries"},new McpToolDefinition{Name="topsolid_create_cylinder"},
                new McpToolDefinition{Name=CamColorPreparation.Apply}}).ToArray();
            var exposure=new ToolExposure(catalog,"create a cylinder",context:context);
            Check.Equal("topsolid_create_cylinder",exposure.Active[0].Name,"Context outranked explicit request");
            Check.True(!exposure.Catalog.Contains(CamColorPreparation.Apply),"Review-only write leaked to generic model path");
            var cancelled=new Client();using var cancelModel=new FakeAiProvider {Reply=(_,_,_)=>Task.FromResult(new AiReply {ToolCalls=[new(){Id="proposal",Name=CamColorPreparation.Propose,Arguments=Proposal()}]})};
            using var cancellation=new CancellationTokenSource();
            var cancelledSession=new ChatSession(cancelModel,cancelled) {ReviewCamColorsAsync=(p,_)=>{cancellation.Cancel();return Task.FromResult<CamColorPlan?>(p);},ConfirmChangeAsync=(_,_)=>Task.FromResult(true)};
            await Check.ThrowsAsync<OperationCanceledException>(()=>cancelledSession.SendAsync("Prepare CAM colors for this part",cancellation.Token));
            Check.Equal(0,cancelled.Prepares,"Cancellation after review still prepared a write");Check.Equal(0,cancelled.Writes,"Cancelled color review wrote native colors");
            var invalid=new Client();using var badModel=new FakeAiProvider {Reply=(_,_,_)=>Task.FromResult(new AiReply {ToolCalls=[new(){Id="bad",Name=CamColorPreparation.Propose,Arguments=bad}]})};
            await new ChatSession(badModel,invalid) {ReviewCamColorsAsync=(_,_)=>throw new Exception("Invalid proposal reached review"),ConfirmChangeAsync=(_,_)=>Task.FromResult(true)}
                .SendAsync("Prepare CAM colors for this part",CancellationToken.None);
            Check.Equal(0,invalid.Writes,"Invented geometry was colored");
            await VerifyTurnContextSnapshot();
        }
        finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    private static async Task VerifyTurnContextSnapshot()
    {
        var context=new StudioContextOptions {Cad=true};
        using var model=new FakeAiProvider();var client=new FakeMcpClient();
        var session=new ChatSession(model,client){ContextOptions=context};
        model.Reply=(round,messages,_)=>{
            if(round==1) {context.Cad=false;context.Cam=true;return Task.FromResult(FakeAiProvider.ToolReply(FakeAiProvider.StatusCall()));}
            return Task.FromResult(new AiReply {Content="Ready."});
        };
        await session.SendAsync("Tell me the status",CancellationToken.None);
        foreach(var request in model.Requests) {
            var system=request.First(m=>m.Role=="system").Content;
            Check.True(system.Contains("CAD context enabled")&&!system.Contains("CAM context enabled"),"Modes changed within an active turn");
        }
        using var replacement=new FakeAiProvider();var changedProvider=new ChatSession(replacement,client){ContextOptions=context.Snapshot()};
        changedProvider.RestoreConversation(session.GetConversationSnapshot());
        await changedProvider.SendAsync("Hello",CancellationToken.None);
        Check.True(replacement.Requests[0].First(m=>m.Role=="system").Content.Contains("CAM context enabled"),"Provider replacement lost context");
    }
    internal static JObject Proposal()=>JObject.Parse("{assignments:[{targetKey:'g1',roleKey:'facing',group:'Top',reason:'Verified planar region; review before coloring.'}]}");
    internal static CamColorPlan Fixture()=>new(){DocumentId="rev",DocumentName="Fixture part",Geometry=new JArray(new JObject {
        ["key"]="native-target",["name"]="Fixture sketch",["kind"]="sketch2d",["target"]=JObject.Parse("{element:{documentId:'rev',id:7}}"),
        ["fingerprint"]=new string('A',64),["color"]=JObject.Parse("{r:1,g:2,b:3}"),["colorSupported"]=true,
        ["geometry"]=JObject.Parse("{colorScope:'wholeSketch',profiles:[{closed:true}],bounds:[0,0,0,0.02,0.01,0]}")})};
    internal sealed class Client : IConfirmableMcpClient
    {
        public bool IsConnected=>true;
        public IReadOnlyList<McpToolDefinition> Tools=>[new(){Name=CamColorPreparation.Inspect,Annotations=new JObject{["readOnlyHint"]=true}},new(){Name=CamColorPreparation.Apply}];
        internal int Writes,Prepares;internal bool Incomplete;
        public Task<McpToolResult> CallToolAsync(string name,JObject arguments,CancellationToken token)=>Task.FromResult(new McpToolResult{StructuredContent=new JObject{
            ["documentId"]="rev",["name"]="Fixture part",["items"]=Fixture().Geometry,["offset"]=0,["total"]=Incomplete?2:1,["hasMore"]=false}});
        public Task<JObject> PrepareToolAsync(string name,JObject args,CancellationToken token) {Prepares++;return Task.FromResult(new JObject{
            ["toolName"]=name,["arguments"]=args.DeepClone(),["confirmationToken"]="test-ticket",["target"]=new JObject{["documentId"]="rev"}});}
        public Task<McpToolResult> CallConfirmedToolAsync(string name,JObject args,string ticket,CancellationToken token)
        {Writes++;return Task.FromResult(new McpToolResult{StructuredContent=new JObject{["readBackVerified"]=true,["saved"]=false,["documentId"]="rev"}});}
    }
}
