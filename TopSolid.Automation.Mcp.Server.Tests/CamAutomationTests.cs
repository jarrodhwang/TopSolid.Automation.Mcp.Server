using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void CamAutomationContracts()
        {
            var method = new CamMethodDefinition { PdmObjectId = "method-pdm", MethodDocumentId = "method-rev", Name = "Test method", Process = "Facing", Colors = CamColorStandard.Starter() };
            var target = JObject.Parse("{face:{element:{documentId:'rev',id:7},label:{type:1,id:2}}}");
            var workpiece = JObject.Parse("{documentId:'rev',id:5}");
            var entry = new JObject { ["target"] = target.DeepClone(), ["fingerprint"] = new string('A',64), ["originalColor"] = method.Colors.Roles[0].Rgb,
                ["roleKey"] = "facing", ["group"] = "Top" };
            var colors = new JObject { ["documentId"] = "rev", ["workpiece"] = workpiece.DeepClone(), ["palette"] = JObject.FromObject(method.Colors), ["targets"] = new JArray(entry) };
            var args = new JObject { ["documentId"] = "rev", ["workpiece"] = workpiece.DeepClone(), ["machiningStage"] = JObject.Parse("{documentId:'rev',id:10}"),
                ["method"] = JObject.FromObject(method), ["colors"] = colors, ["executionId"] = Guid.NewGuid().ToString("N") };
            CamMethodTools.Validate(args);
            foreach (var change in new Action<JObject>[] {
                p => p["executionId"] = "../retry", p => p["method"]["Options"]["SilentMode"] = "true",
                p => ((JObject)p["method"]["Options"]).Remove("KeepAssociativity"), p => p["method"]["executeUnregistered"] = true,
                p => p["colors"]["documentId"] = "other", p => p["colors"]["workpiece"]["id"] = 6,
                p => p["method"]["Colors"]["Version"] = 2, p => p["colors"]["targets"][0]["target"]["face"]["element"]["documentId"] = "foreign"
            }) { var invalid=(JObject)args.DeepClone(); change(invalid); Throws<ArgumentException>(()=>CamMethodTools.Validate(invalid)); }

            var approved=(JArray)colors["targets"]; var rgb=method.Colors.Roles[0].Rgb;
            CamMethodTools.VerifyScopeTarget(approved,method.Colors,target,rgb);
            var foreign=(JObject)target.DeepClone(); foreign["face"]["element"]["id"]=8;
            Throws<InvalidOperationException>(()=>CamMethodTools.VerifyScopeTarget(approved,method.Colors,foreign,rgb));
            CamMethodTools.VerifyScopeTarget(approved,method.Colors,foreign,JObject.Parse("{r:1,g:2,b:3}"));
            var shape=new JObject { ["element"]=target["face"]["element"].DeepClone() };
            // Approving a face must not authorize a whole shape with the same RGB.
            Throws<InvalidOperationException>(()=>CamMethodTools.VerifyScopeTarget(approved,method.Colors,shape,rgb));
            var whole=new JArray(new JObject { ["target"]=shape, ["roleKey"]="facing" });
            CamMethodTools.VerifyScopeTarget(whole,method.Colors,target,rgb);
            Throws<InvalidOperationException>(()=>CamMethodTools.VerifyScopeTarget(whole,method.Colors,target,method.Colors.Roles[1].Rgb));
            Check((double)CamMethodTools.Input(new CamMethodInput { Name="Depth", ValueType="real", UnitType="Length", Value=new JValue(.012) })["realValueSI"] == .012,"Method input lost SI units");
            Throws<ArgumentException>(()=>CamMethodTools.Input(new CamMethodInput { Name="Speed",ValueType="real",UnitType="AngularSpeed" }));

            var mesh=JObject.Parse("{positions:[0,0,0,1,0,0,0,1,0],indices:[0,1,2]}"); var bounds=new JArray(0,0,0,1,1,0);
            AutomationGateway.VerifyMesh(mesh,bounds);
            foreach(var change in new Action<JObject>[] { p=>p["positions"][0]=double.NaN,p=>p["positions"][0]=2,p=>p["indices"][1]=3,p=>p["indices"][1]=1.5,
                p=>p["positions"][3]=.5,p=>p["indices"]=new JArray() }) {
                var bad=(JObject)mesh.DeepClone();change(bad);Throws<InvalidDataException>(()=>AutomationGateway.VerifyMesh(bad,bounds));
            }
            Check(CamStages.IsMachining("TopSolid.Cam.NC.Kernel.DB.Stages.MachiningStageOperation") && !CamStages.IsMachining("가공 스테이지"),"Stage classification uses display names");
            using(var gateway=new AutomationGateway()) {
                var registry=new ToolRegistry(gateway);
                Throws<RpcException>(()=>registry.Call(CamMethodTools.ExecuteName,args));
                var schema=(JObject)registry.List().Single(t=>(string)t["name"]==CamMethodTools.ExecuteName)["inputSchema"];
                Schema.Validate(args,schema);
                foreach(var spec in new[] { new {Name="topsolid_create_cylinder",Category="Design3D",Intent=EditStage.Modeling},
                    new {Name=CamMethodTools.ExecuteName,Category="Cam/Operation",Intent=EditStage.Machining},
                    new {Name="topsolid_delete_elements",Category="Entities",Intent=EditStage.Target},
                    new {Name="topsolid_update_elements",Category="Entities",Intent=EditStage.Target},
                    new {Name=CamColorTools.ApplyName,Category="Cam/PartSetup",Intent=EditStage.Modeling} }) {
                    var tool=new ToolDefinition(spec.Name,"Test",new JObject(),_=>new JObject(),spec.Category,new string[0],false);
                    Check(ToolRegistry.StageIntent(tool)==spec.Intent,"Wrong required stage for "+spec.Name);
                }
            }
            ColorFacesAcrossShapes();
            Console.WriteLine("PASS CAM method approval, exact scope/RGB, stage routing and native face mesh contracts.");
        }
        private static void ColorFacesAcrossShapes()
        {
            var rows=new[] {7,8}.Select(id=>JObject.Parse("{target:{face:{element:{documentId:'rev',id:"+id+"},label:{type:1,id:2}}},color:{empty:true},colorSupported:true}")).ToArray();
            foreach(var row in rows) row["fingerprint"]=CamColorGeometry.Fingerprint(row);
            var args=new JObject { ["documentId"]="rev",["palette"]=JObject.FromObject(CamColorStandard.Starter()),
                ["targets"]=new JArray(rows.Select(row=>new JObject { ["target"]=row["target"].DeepClone(),["fingerprint"]=row["fingerprint"].DeepClone(),["originalColor"]=row["color"].DeepClone(),["roleKey"]="facing",["group"]="Top" })) };
            int nativeCalls=0;
            var result=CamColorTools.ApplyVerified(args,target=>rows.Single(row=>JToken.DeepEquals(row["target"],target)),
                (_,__)=>throw new Exception("Unexpected whole-entity write"),(targets,rgb)=> {
                    Check(targets.Select(t=>(int)t["face"]["element"]["id"]).Distinct().Count()==1,"One native coloring operation crossed shape identities");nativeCalls++;
                    foreach(var target in targets) rows.Single(row=>JToken.DeepEquals(row["target"],target))["color"]=rgb.DeepClone();
                    return new JObject { ["id"]=nativeCalls };
                });
            Check(nativeCalls==2 && (bool)result["readBackVerified"],"Multiple shape colors were not verified in one reviewed batch");
        }
    }
}
