using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void SketchReliability()
        {
            var input = JObject.Parse("{kind:'heart',center:{x:12,y:-5},width:100,height:80,rotationDegrees:0}");
            var heart = SketchCurveGeometry.Parse(input, .001);
            Check(heart.Closed && heart.SegmentCount == 6 && heart.Points.Length == 18, "Heart must have six cubics sharing closure");
            var samples = Enumerable.Range(0, 6).SelectMany(i => Enumerable.Range(0, 101).Select(j => SketchCurveGeometry.Cubic(heart.CubicSegment(i), j / 100.0))).ToArray();
            Check(Math.Abs(samples.Max(p => p.X) - samples.Min(p => p.X) - .1) < 1e-12 && Math.Abs(samples.Max(p => p.Y) - samples.Min(p => p.Y) - .08) < 1e-12, "Heart dimensions do not describe exact bounds");
            foreach (var point in samples)
                Check(samples.Any(p => Math.Abs(p.X + point.X - .024) < 1e-12 && Math.Abs(p.Y - point.Y) < 1e-12), "Heart is not symmetric about requested center");
            for (var i = 0; i < 6; i++)
                Check(SketchCurveGeometry.Distance(heart.CubicSegment(i)[3], heart.CubicSegment((i + 1) % 6)[0]) == 0, "Heart spans do not share exact endpoints");
            input["rotationDegrees"] = 90;
            var rotated = SketchCurveGeometry.Parse(input, .001);
            for (var i = 0; i < heart.Points.Length; i++)
                Check(Math.Abs(rotated.Points[i].X - (.012 - (heart.Points[i].Y + .005))) < 1e-12 &&
                    Math.Abs(rotated.Points[i].Y - (-.005 + heart.Points[i].X - .012)) < 1e-12, "Heart rotation moved the center or changed shape");
            input["width"] = 0; Throws<ArgumentException>(() => SketchCurveGeometry.Parse(input, .001));
            Throws<ArgumentException>(() => SketchCurveGeometry.Parse(JObject.Parse("{kind:'polyline',closed:false,points:[{x:0,y:0},{x:20,y:0},{x:0,y:20},{x:0,y:0}]}"), .001));
            using (var gateway = new AutomationGateway())
            {
                var registry = new ToolRegistry(gateway); var catalog = registry.List();
                Check((bool)catalog.Single(t => (string)t["name"] == "topsolid_create_sketch_section")["_meta"]["topsolid/requiresConfirmation"], "Explicit section action needs separate confirmation");
                foreach (var definition in catalog) {
                    var schema = definition["inputSchema"].ToString();
                    Check(!schema.Contains("\"createSection\"") && !schema.Contains("\"sectionMode\""), "A schema still offers section creation");
                }
                var heartSchema = (JObject)catalog.Single(t => (string)t["name"] == "topsolid_create_heart_sketch")["inputSchema"];
                var args = JObject.Parse("{documentId:'rev',name:'Heart',placement:'xz',width:100,height:80,center:{x:10,y:20,z:30},rotationDegrees:90}");
                Schema.Validate(args, heartSchema); var expanded = HeartSketchTools.Expand(args); SketchBatchActionTools.ValidateProfiles(expanded);
                Check(JToken.DeepEquals(expanded["origin"], args["center"]) && (string)expanded["placement"] == "xz", "Compact heart changed requested world center or plane");
                Check(Throws<RpcException>(() => registry.Call("topsolid_create_heart_sketch", args)).Code == -32010, "Heart creation bypassed confirmation");
                args["createSection"] = true; Throws<RpcException>(() => Schema.Validate(args, heartSchema));
            }
            foreach (var scenario in new[] { "unique", "duplicateDoc", "duplicateProject", "incomplete", "external" })
            {
                var context = JObject.Parse("{complete:true,projectMatches:[{pdmObjectId:'project'}],documents:[{documentId:'revision'}],errors:[]}");
                if (scenario == "duplicateDoc") ((JArray)context["documents"]).Add(context["documents"][0].DeepClone());
                if (scenario == "duplicateProject") ((JArray)context["projectMatches"]).Add(context["projectMatches"][0].DeepClone());
                if (scenario == "incomplete") context["complete"] = false;
                if (scenario == "external") context["documents"][0]["documentId"] = null;
                Check((bool)ModelingContextTools.Finish(context)["canChooseUniqueDocument"] == (scenario == "unique"), "Context silently chose an unsafe target: " + scenario);
            }
        }
    }
}
