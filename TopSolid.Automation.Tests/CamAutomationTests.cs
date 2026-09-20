using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class CamAutomationTests
{
    internal static async Task Run()
    {
        await CamMethodBrowserTests.Run();
        var options = new CamMethodOptions();
        Check.True(options.KeepAssociativity && options.UseCuttingConditions && !options.LaunchDeferred && !options.ManualExecution && !options.ReuseAnswers && !options.SilentMode, "Native execution defaults changed");
        Check.True(CamAutomationPreparation.Matches("이 부품 형상 분석해줘", new() { Cam = true }), "Color-free Korean analysis routing");
        Check.True(!CamAutomationPreparation.Matches("Explain how CAM analysis works", new() { Cam = true }), "Explanation became an execution workflow");
        Check.True(!CamAutomationPreparation.Matches("이 부품 형상 분석해줘", new() { Cad = true }), "CAD-only analysis was intercepted");
        Check.True(!CamAutomationPreparation.Matches("Analyze this part and prepare CAM colors", new() { Cam = true }), "Manual color route lost");
        Check.True(CamAutomationPreparation.ResumeMatches("CAM 재개") && !CamAutomationPreparation.ResumeMatches("CAM 재개하지 마"), "Resume intent routing");
        var method = Method(); method.Validate();
        var duplicate = method.Snapshot(); duplicate.Inputs = [new() { Name = "feed", ValueType = "real", UnitType = "Speed", Value = new JValue(1.0) }, new() { Name = "feed" }];
        Check.Throws<ArgumentException>(duplicate.Validate);
        var input = new CamMethodInput { Name = "feed", ValueType = "real", Value = new JValue("fast") }; Check.Throws<ArgumentException>(input.Validate);
        var directory = Path.Combine(Path.GetTempPath(), "Studio-cam-automation-" + Guid.NewGuid().ToString("N"));
        try {
            var settings = new SettingsStore(directory); settings.SaveCamMethods([method]);
            Check.Equal(method.MethodDocumentId, settings.Load().CamMethods.Single().MethodDocumentId, "Method catalog restart without main settings");
            settings.SaveContextOptions(new() { Cam = true }); Check.Equal(method.Id, settings.Load().CamMethods.Single().Id, "Mode save overwrote method catalog");
            var second = Method("second"); method.AfterMethods = [second.Id]; second.AfterMethods = [method.Id]; Check.Throws<ArgumentException>(() => CamMethodCatalog.Validate([method, second])); method.AfterMethods.Clear();
            var state = new CamAutomationStore(directory); var fixture = Plan(); state.Save(fixture); Check.Equal(fixture.Id, state.Load()!.Id, "Prepared plan restart");
            var stale = fixture.Snapshot(); stale.Steps[0].Colors.Geometry[0]!["colorSupported"] = false; Check.Throws<ArgumentException>(stale.Validate);
            var altered = fixture.Snapshot(); altered.Steps[0].Method.Options.LaunchDeferred = true; CamAutomationPreparation.ValidateExecutionApproval(fixture, altered);
            altered.Steps[0].Method.MethodDocumentId = "other"; Check.Throws<ArgumentException>(() => CamAutomationPreparation.ValidateExecutionApproval(fixture, altered));
            var rebase = fixture.Snapshot(); CamAutomationGeometry.Rebase(rebase, "rev-new"); rebase.Validate();
            Check.Equal("rev-new", (string)rebase.Steps[0].Colors.ToArguments()["targets"]![0]!["target"]!["face"]!["element"]!["documentId"]!, "Geometry target was not rebased");
            Check.Equal("method-rev", rebase.Steps[0].Method.MethodDocumentId, "Rebase changed external method revision");
            VerifyModelBoundary(); VerifyFaceMapping();
            foreach (var review in new[] { false, true }) foreach (var colorApproval in new[] { false, true }) foreach (var methodApproval in new[] { false, true })
            {
                var client = new Client(); using var model = Model(); var confirms = 0;
                var session = Session(model, client);
                session.ReviewCamAutomationAsync = (p, _) => Task.FromResult(review ? p : null);
                session.ConfirmChangeAsync = (_, _) => Task.FromResult(colorApproval);
                session.ConfirmCamMethodsAsync = (p, _) => { confirms++; return Task.FromResult(methodApproval ? p : null); };
                await session.SendAsync("이 부품 형상 분석해줘", CancellationToken.None);
                Check.Equal(review && colorApproval ? 1 : 0, client.ColorWrites, "Color review/Access boundary");
                Check.Equal(review && colorApproval && methodApproval ? 1 : 0, client.MethodWrites, "Mandatory method confirmation boundary");
                Check.Equal(review && colorApproval ? 1 : 0, confirms, "Method dialog appeared before prepared colors");
                Check.True(model.SuppliedTools.All(t => t.SequenceEqual(new[] { CamAutomationPreparation.Proposal })), "Planning model received native mutation tools");
            }
            await ResumeAndStaleness(); await MultipleProcesses(); await UnknownOutcome();
            var proposal = new JObject { ["toolName"] = CamAutomationPreparation.ExecuteMethod, ["arguments"] = new JObject(), ["target"] = new JObject { ["documentId"] = "rev" } };
            foreach (var permission in Enum.GetValues<PermissionMode>()) Check.True(PermissionPolicy.Evaluate(permission, proposal).RequiresApproval, "Method bypassed confirmation under " + permission);
            var tools = new ToolExposure([new McpToolDefinition { Name = CamAutomationPreparation.ExecuteMethod }], "execute method"); Check.True(!tools.Catalog.Contains(CamAutomationPreparation.ExecuteMethod), "Generic model can bypass method registration/review");
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private static void VerifyModelBoundary()
    {
        var plan = Plan(); plan.Steps.Clear(); var aliases = new Dictionary<string, JObject> { ["g1"] = (JObject)plan.Geometry[0]! };
        var methods = new Dictionary<string, CamMethodDefinition> { ["m1"] = Method() };
        var invalid = Proposal(); invalid["processes"]![0]!["methodKey"] = "invented";
        Check.Throws<ArgumentException>(() => CamAutomationPreparation.AddProposals(plan, invalid, aliases, methods));
        invalid = Proposal(); invalid["processes"]![0]!["assignments"]![0]!["targetKey"] = "invented";
        Check.Throws<ArgumentException>(() => CamAutomationPreparation.AddProposals(plan, invalid, aliases, methods));
        var uncertain = Plan(); uncertain.Steps.Clear(); CamAutomationPreparation.AddProposals(uncertain, JObject.Parse("{processes:[],unassigned:[{targetKey:'g1',reason:'Insufficient evidence of a hole'}]}"), aliases, methods);
        Check.Equal(0, uncertain.Steps.Count, "Ambiguous geometry acquired a process");
    }
    private static void VerifyFaceMapping()
    {
        var row = Geometry(); var bytes = Mesh(row);
        var display = CamFaceGeometry.Decode(bytes, "rev", [row], CancellationToken.None); Check.Equal(1, display.Single().Scene!.Triangles, "Native mesh decode");
        var forged = JObject.Parse(Encoding.UTF8.GetString(bytes)); forged["items"]![0]!["face"]!["label"]!["id"] = 99;
        Check.Throws<InvalidDataException>(() => CamFaceGeometry.Decode(Encoding.UTF8.GetBytes(forged.ToString()), "rev", [row], CancellationToken.None));
        forged = JObject.Parse(Encoding.UTF8.GetString(bytes)); forged["items"]![0]!["fingerprint"] = "stale";
        Check.Throws<InvalidDataException>(() => CamFaceGeometry.Decode(Encoding.UTF8.GetBytes(forged.ToString()), "rev", [row], CancellationToken.None));
        forged = JObject.Parse(Encoding.UTF8.GetString(bytes)); forged["items"]![0]!["indices"]![2] = 999;
        Check.Throws<InvalidDataException>(() => CamFaceGeometry.Decode(Encoding.UTF8.GetBytes(forged.ToString()), "rev", [row], CancellationToken.None));
        var unavailable = JObject.Parse(Encoding.UTF8.GetString(bytes)); var missing = (JObject)unavailable["items"]![0]!;
        missing.Remove("positions"); missing.Remove("indices"); missing["status"] = "unavailable"; missing["reason"] = "Native display is not current.";
        var excluded = CamFaceGeometry.Decode(Encoding.UTF8.GetBytes(unavailable.ToString()), "rev", [row], CancellationToken.None).Single();
        Check.True(excluded.Scene == null && excluded.Reason == "Native display is not current.", "Unmapped face was rendered or lost its exclusion reason");
        missing["face"]!["label"]!["id"] = 99;
        Check.Throws<InvalidDataException>(() => CamFaceGeometry.Decode(Encoding.UTF8.GetBytes(unavailable.ToString()), "rev", [row], CancellationToken.None));
    }
    internal static byte[] Mesh(JObject row) => Encoding.UTF8.GetBytes(new JObject { ["version"] = 1, ["documentId"] = "rev", ["units"] = "m", ["items"] = new JArray(new JObject {
        ["key"] = row["key"], ["fingerprint"] = row["fingerprint"], ["face"] = row["target"]!["face"], ["color"] = row["color"],
        ["positions"] = new JArray(0, 0, 0, .01, 0, 0, 0, .01, 0), ["indices"] = new JArray(0, 1, 2) }) }.ToString(Formatting.None));
    private static async Task ResumeAndStaleness()
    {
        foreach (var stale in new[] { "none", "color", "geometry" })
        {
            var client = new Client(); using var model = Model(); var session = Session(model, client);
            session.ConfirmCamMethodsAsync = (_, _) => Task.FromResult<CamAutomationPlan?>(null);
            await session.SendAsync("Analyze this CAM part", CancellationToken.None); Check.Equal(1, client.ColorWrites, "Preparation not retained after cancelled launch");
            session.Clear(); Check.True(session.PreparedCamPlan != null && session.ContextOptions.Cam, "Clear discarded pending CAM plan or context");
            if (stale == "color") client.Row["color"] = JObject.Parse("{r:1,g:2,b:3}");
            if (stale == "geometry") client.Row["geometryFingerprint"] = new string('B', 64);
            session.ConfirmCamMethodsAsync = (p, _) => Task.FromResult<CamAutomationPlan?>(p);
            await session.SendAsync("CAM 재개", CancellationToken.None);
            Check.Equal(stale == "none" ? 1 : 0, client.MethodWrites, "Resume failed to reject changed " + stale);
            Check.Equal(1, client.ColorWrites, "Resume reapplied prepared colors"); Check.Equal(1, model.CompletionCount, "Resume replanned with AI");
        }
    }
    private static async Task MultipleProcesses()
    {
        foreach (var associative in new[] { true, false }) foreach (var deferred in new[] { false, true })
        {
            var client = new Client(); using var model = Model(true); var session = Session(model, client);
            session.CamMethods = [Method(), Method("second")];
            session.ConfirmCamMethodsAsync = (p, _) => { p.Steps[0].Method.Options.KeepAssociativity = associative; p.Steps[0].Method.Options.LaunchDeferred = deferred; return Task.FromResult<CamAutomationPlan?>(p); };
            await session.SendAsync("Analyze this CAM part", CancellationToken.None);
            Check.Equal(associative || deferred ? 1 : 2, client.MethodWrites, "Unsafe recolor reached subsequent method");
            Check.Equal(associative || deferred ? 1 : 2, client.ColorWrites, "Recolor ordering");
            Check.True(client.Actions.Take(2).SequenceEqual(new[] { "colors", "method" }), "Method ran before preparation");
        }
    }
    private static async Task UnknownOutcome()
    {
        var client = new Client { FailExecution = true }; using var model = Model(); var session = Session(model, client);
        await session.SendAsync("Analyze this CAM part", CancellationToken.None); await session.SendAsync("CAM 재개", CancellationToken.None);
        Check.Equal(1, client.MethodWrites, "Unknown native execution was retried");
    }
    private static ChatSession Session(IAiProvider model, Client client) => new(model, client) { ContextOptions = new() { Cam = true }, CamMethods = [Method()],
        ReviewCamAutomationAsync = (p, _) => Task.FromResult<CamAutomationPlan?>(p), ConfirmChangeAsync = (_, _) => Task.FromResult(true), ConfirmCamMethodsAsync = (p, _) => Task.FromResult<CamAutomationPlan?>(p) };
    private static FakeAiProvider Model(bool two = false) => new() { Reply = (_, _, _) => Task.FromResult(new AiReply { ToolCalls = [new() { Id = "proposal", Name = CamAutomationPreparation.Proposal, Arguments = Proposal(two) }] }) };
    private static JObject Proposal(bool two = false)
    {
        var data = JObject.Parse("{processes:[{methodKey:'m1',reason:'Verified planar surface',assignments:[{targetKey:'g1',roleKey:'facing',reason:'Planar surface'}]}],unassigned:[]}");
        if (two) ((JArray)data["processes"]!).Add(JObject.Parse("{methodKey:'m2',reason:'Finishing',assignments:[{targetKey:'g1',roleKey:'finish',reason:'Same verified surface'}]}")); return data;
    }
    internal static CamMethodDefinition Method(string id = "first") => new() { Id = id, PdmObjectId = "method-pdm-" + id, MethodDocumentId = "method-rev", Name = "Fixture " + id, Process = id == "first" ? "Facing" : "Finishing", Conditions = "Verified planar face", Colors = CamColorStandard.Starter() };
    internal static JObject Geometry()
    {
        var row = JObject.Parse("{target:{face:{element:{documentId:'rev',id:11},label:{type:70,id:1}}},name:'Fixture face',kind:'face',color:{empty:true},colorSupported:true,geometry:{surfaceType:'Plane',bounds:[0,0,0,0.01,0.01,0]}}");
        row["key"] = row["target"]!.ToString(Formatting.None); row["fingerprint"] = new string('A', 64); row["geometryFingerprint"] = new string('C', 64); return row;
    }
    internal static CamAutomationPlan Plan()
    {
        var row = Geometry(); var plan = new CamAutomationPlan { DocumentId = "rev", DocumentName = "Disposable CAM fixture", Workpiece = JObject.Parse("{documentId:'rev',id:10}"),
            ModelingStage = JObject.Parse("{documentId:'rev',id:2}"), MachiningStage = JObject.Parse("{documentId:'rev',id:3}"), Geometry = new JArray(row) };
        plan.Steps.Add(new CamAutomationStep { Method = Method(), Colors = new CamColorPlan { DocumentId = "rev", Workpiece = (JObject)plan.Workpiece.DeepClone(), ModelingStage = (JObject)plan.ModelingStage.DeepClone(), Geometry = new JArray(row.DeepClone()),
            Assignments = [new() { TargetKey = (string)row["key"]!, RoleKey = "facing", Group = "Facing", Reason = "Verified plane" }] } }); return plan;
    }
    internal sealed class Client : IConfirmableMcpClient
    {
        public bool IsConnected => true;
        public IReadOnlyList<McpToolDefinition> Tools { get; } = [new() { Name = CamAutomationPreparation.ExecuteMethod, Annotations = new JObject { ["readOnlyHint"] = false } }];
        internal JObject Row = Geometry(); internal int ColorWrites, MethodWrites; internal bool FailExecution; internal List<string> Actions = [];
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken token)
        {
            JObject data = name switch {
                "topsolid_inspect_cam_method" => new JObject { ["pdmObjectId"] = args["pdmObjectId"], ["methodDocumentId"] = "method-rev", ["isDirty"] = false },
                "topsolid_get_cam_stages" => JObject.Parse("{documentId:'rev',isCam:true,modelingStage:{documentId:'rev',id:2},stages:[{element:{documentId:'rev',id:3},name:'Machining',machining:true}]}"),
                CamColorPreparation.Inspect => args["workpiece"] == null ? JObject.Parse("{documentId:'rev',name:'Fixture',needsWorkpiece:true,workpieces:[{element:{documentId:'rev',id:10},name:'Workpiece'}]}") :
                    new JObject { ["documentId"] = "rev", ["name"] = "Fixture", ["items"] = new JArray(Row.DeepClone()), ["offset"] = 0, ["total"] = 1, ["hasMore"] = false },
                _ => throw new InvalidOperationException("Unexpected read " + name) };
            return Task.FromResult(Result(data));
        }
        public Task<JObject> PrepareToolAsync(string name, JObject args, CancellationToken token) => Task.FromResult(new JObject { ["toolName"] = name, ["arguments"] = args.DeepClone(), ["target"] = new JObject { ["documentId"] = "rev" }, ["confirmationToken"] = "approved" });
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject args, string ticket, CancellationToken token)
        {
            if (name == CamColorPreparation.Apply) {
                ColorWrites++; Actions.Add("colors"); var palette = args["palette"]!.ToObject<CamColorStandard>()!;
                Row["color"] = palette.Roles.Single(r => r.Key == (string)args["targets"]![0]!["roleKey"]!).Rgb; Row["fingerprint"] = new string('D', 64);
                return Task.FromResult(Result(new JObject { ["documentId"] = "rev", ["readBackVerified"] = true, ["saved"] = false }));
            }
            MethodWrites++; Actions.Add("method"); if (FailExecution) return Task.FromResult(new McpToolResult { IsError = true, StructuredContent = new JObject { ["detail"] = "Native failure" } });
            return Task.FromResult(Result(new JObject { ["documentId"] = "rev", ["executionId"] = args["executionId"], ["status"] = (bool?)args["method"]?["Options"]?["LaunchDeferred"] == true ? "deferred" : "calculated", ["operations"] = new JArray(new JObject { ["name"] = "Fixture operation", ["upToDate"] = true }), ["saved"] = false }));
        }
        private static McpToolResult Result(JObject data) => new() { StructuredContent = data };
    }
}
