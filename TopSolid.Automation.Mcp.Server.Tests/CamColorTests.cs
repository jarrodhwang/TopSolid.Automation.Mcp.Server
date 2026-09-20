using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void CamColorPlans()
        {
            var row=JObject.Parse("{target:{element:{documentId:'rev',id:7}},name:'Sketch',kind:'sketch2d',color:{r:1,g:2,b:3},colorSupported:true,geometry:{vertices:[{X:1,Y:2}]}}");
            row["fingerprint"]=CamColorGeometry.Fingerprint(row);
            var entry=new JObject{["target"]=row["target"].DeepClone(),["fingerprint"]=row["fingerprint"].DeepClone(),["originalColor"]=row["color"].DeepClone(),["roleKey"]="facing",["group"]="Top"};
            var input=new JObject{["documentId"]="rev",["palette"]=JObject.FromObject(CamColorStandard.Starter()),["targets"]=new JArray(entry)};
            Check(CamColorTools.Verify(input,_=>(JObject)row.DeepClone()).Count==1,"Verified color plan rejected");
            var oldFingerprint=(string)row["fingerprint"];
            row["geometry"]["vertices"][0]["X"]=3;row["fingerprint"]=CamColorGeometry.Fingerprint(row);
            Throws<InvalidOperationException>(()=>CamColorTools.Verify(input,_=>row));
            row["fingerprint"]=oldFingerprint;row["color"]["r"]=9;Throws<InvalidOperationException>(()=>CamColorTools.Verify(input,_=>row));row["color"]["r"]=1;
            row["colorSupported"]=false;Throws<ArgumentException>(()=>CamColorTools.Verify(input,_=>row));row["colorSupported"]=true;
            var duplicate=(JObject)input.DeepClone();((JArray)duplicate["targets"]).Add(entry.DeepClone());Throws<ArgumentException>(()=>CamColorTools.Validate(duplicate));
            duplicate["targets"][1]["target"]["element"]=JObject.Parse("{id:7,documentId:'rev'}");Throws<ArgumentException>(()=>CamColorTools.Validate(duplicate));
            var foreign=(JObject)input.DeepClone();foreign["targets"][0]["target"]["element"]["documentId"]="other";Throws<ArgumentException>(()=>CamColorTools.Validate(foreign));
            var unknown=(JObject)input.DeepClone();unknown["targets"][0]["roleKey"]="invented";Throws<ArgumentException>(()=>CamColorTools.Validate(unknown));
            var rebased=MutationReferences.Rebase(new JObject{["documentId"]="rev",["row"]=row.DeepClone()},"new-revision");
            Check(CamColorGeometry.Fingerprint((JObject)rebased["row"])==CamColorGeometry.Fingerprint(row),"Dirty revision changed geometry fingerprint");
            var excessive=new JArray(Enumerable.Range(0,33).Select(_=>entry.DeepClone()));Throws<ArgumentException>(()=>CamColorPlan.ValidateCounts(excessive));
            Check(CamColorGeometry.MatchesFinishReference("rev::5938","rev",5938),"Native CAM finish reference did not resolve");
            Check(!CamColorGeometry.MatchesFinishReference("other::5938","rev",5938) && !CamColorGeometry.MatchesFinishReference("rev::59380","rev",5938),"CAM finish scope accepted a foreign or partial identity");
            ColorTransactionReadback();
            using(var gateway=new AutomationGateway()) {
                var registry=new ToolRegistry(gateway);
                Throws<RpcException>(()=>registry.Call(CamColorTools.ApplyName,input));
                var schema=(JObject)registry.List().Single(t=>(string)t["name"]==CamColorTools.ApplyName)["inputSchema"];
                Schema.Validate(input,schema);
                var edge=(JObject)input.DeepClone();edge["targets"][0]["target"]=JObject.Parse("{edge:{element:{documentId:'rev',id:7},label:{type:1,id:2}}}");
                Throws<RpcException>(()=>Schema.Validate(edge,schema));
            }
            Console.WriteLine("PASS CAM color scope, fingerprints, RGB, palette roles, batch limits and unapproved writes.");
        }
        private static void ColorTransactionReadback()
        {
            var shape=JObject.Parse("{target:{element:{documentId:'rev',id:7}},kind:'shape',color:{r:1,g:2,b:3},colorSupported:true,geometry:{volume:0.01}}");
            var face=JObject.Parse("{target:{face:{element:{documentId:'rev',id:7},label:{type:1,id:2}}},kind:'face',color:{r:255,g:0,b:0},colorSupported:true,geometry:{area:0.1}}");
            foreach(var row in new[]{shape,face}) row["fingerprint"]=CamColorGeometry.Fingerprint(row);
            var args=new JObject { ["documentId"]="rev",["palette"]=JObject.FromObject(CamColorStandard.Starter()),["targets"]=new JArray(new[]{face,shape}.Select(r=>new JObject {
                ["target"]=r["target"].DeepClone(),["fingerprint"]=r["fingerprint"].DeepClone(),["originalColor"]=r["color"].DeepClone(),["roleKey"]=r==shape?"roughing":"facing",["group"]="Fixture" })) };
            foreach(var failReadback in new[]{false,true}) {
                var currentShape=(JObject)shape.DeepClone();var currentFace=(JObject)face.DeepClone();
                var writes=new global::System.Collections.Generic.List<string>();var rolledBack=false;var committed=false;
                JObject Run()=>ModificationScope.Run("Fixture",_=>true,(commit,update)=>{
                    committed=commit; if(!commit) { rolledBack=true;currentShape=(JObject)shape.DeepClone();currentFace=(JObject)face.DeepClone(); }
                },()=>CamColorTools.ApplyVerified(args,t=>(JObject)(t["face"]==null?currentShape:currentFace).DeepClone(),
                    (t,rgb)=>{writes.Add("entity");currentShape["color"]=rgb.DeepClone();},
                    (targets,rgb)=>{writes.Add("faces");currentFace["color"]=failReadback?new JObject{["r"]=0,["g"]=0,["b"]=0}:rgb.DeepClone();return new JObject{["id"]=99};}));
                if(failReadback) {
                    Throws<InvalidOperationException>(()=>Run());
                    Check(rolledBack&&!committed&&JToken.DeepEquals(shape,currentShape)&&JToken.DeepEquals(face,currentFace),"Failed readback did not restore the entire batch");
                } else {
                    var result=Run();Check((bool)result["readBackVerified"]&&committed&&!rolledBack,"Successful color batch did not commit");
                    Check(JToken.DeepEquals(result["items"][0]["originalColor"],face["color"]),"Color receipt lost original override");
                }
                Check(writes.SequenceEqual(new[]{"entity","faces"}),"Whole-entity colors must precede face overrides regardless of input order");
            }
            var tooManyFaces=new JArray(Enumerable.Range(0,257).Select(_=>args["targets"][0].DeepClone()));
            Throws<ArgumentException>(()=>CamColorPlan.ValidateCounts(tooManyFaces));
            tooManyFaces.RemoveAt(256);CamColorPlan.ValidateCounts(tooManyFaces);
        }
    }
}
