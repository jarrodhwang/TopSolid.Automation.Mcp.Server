using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void SketchPlans()
        {
            var label = AutomationValues.ParseItemLabel(JObject.Parse("{type:118,id:3,moniker:'v145(3)'}"));
            Check(label.Name == null && label.Moniker == "v145(3)", "Absent topology name must remain null, never an empty lookup name");
            var namedLabel = AutomationValues.ParseItemLabel(JObject.Parse("{type:115,id:1,moniker:'s145(1)',name:'native-axis-name'}"));
            Check(namedLabel.Name == "native-axis-name", "Named topology lost its native lookup name");
            Check(JToken.DeepEquals(AutomationValues.Json(label), JObject.Parse("{type:118,id:3,moniker:'v145(3)'}")), "Native label JSON roundtrip changed its identity");
            var names = JObject.Parse("{internalName:'',friendlyName:'Sketch 1'}");
            Check(SketchPlanTools.MatchesName(names, new[] { "Sketch 1" }), "Reference lookup ignores displayed friendly names");
            Check(!SketchPlanTools.MatchesName(names, new[] { "Sketch" }), "Reference lookup must not silently choose a partial-name match");
            names["internalName"] = "Circle Sketch";
            Check(SketchPlanTools.MatchesName(names, new[] { "circle sketch" }), "Reference lookup lost explicit internal names");
            var referenceSpec = JObject.Parse("{placement:'reference',documentSpace:'3d',referenceSketch:{documentId:'rev',id:7}}");
            Throws<ArgumentException>(() => SketchPlacement.Validate(referenceSpec));
            referenceSpec["anchor"] = JObject.Parse("{item:{element:{documentId:'rev',id:7},label:{type:118,id:3}},location:'vertex'}");
            SketchPlacement.Validate(referenceSpec); checks++;
            referenceSpec["rotationDegrees"] = 45; Throws<ArgumentException>(() => SketchPlacement.Validate(referenceSpec)); referenceSpec["rotationDegrees"] = 90;
            referenceSpec["origin"] = JObject.Parse("{x:20,y:30,z:1}"); Throws<ArgumentException>(() => SketchPlacement.Validate(referenceSpec)); referenceSpec["origin"]["z"] = 0;
            SketchPlacement.Validate(referenceSpec); checks++;
            referenceSpec["documentSpace"] = "2d"; Throws<ArgumentException>(() => SketchPlacement.Validate(referenceSpec));
            referenceSpec["referenceMode"] = "snapshot"; SketchPlacement.Validate(referenceSpec); checks++;
            var nativeLink = new SketchAssociativeReference { Sketch = new ElementId(new DocumentId("rev"), 7),
                Anchor = new ElementItemId(new ElementId(new DocumentId("rev"), 7), new ItemLabel(118, 3, "v(3)", null)),
                XAxis = new ElementItemId(new ElementId(new DocumentId("rev"), 7), new ItemLabel(115, 1, "s(1)", "XAxis")) };
            Check(nativeLink.Origin3D().Type == SmartPoint3DType.Item && nativeLink.Origin3D().ElementId.Equals(nativeLink.Sketch), "Associative origin was reduced to basic coordinates");
            Check(nativeLink.Direction3D(false).Type == SmartDirection3DType.Item && nativeLink.Direction3D(false).ItemLabel.Equals(nativeLink.XAxis.ItemLabel), "Associative direction was reduced to a fixed vector");
            // Reproduce the open path from the attached 13:34 log. Its vertices
            // already share endpoints; adding more sampled points cannot fix a
            // profile factory that requires closure in that native session.
            var failedPath = JObject.Parse("{kind:'polyline',closed:false,points:[{x:-50,y:50},{x:-25,y:12.5},{x:0,y:0},{x:25,y:12.5},{x:50,y:50}]}");
            var curve = SketchCurveGeometry.Parse(failedPath, .001);
            Check(!curve.Closed && curve.SegmentCount == 4, "Open path was closed or gained an extra edge");
            var profileCalls = 0;
            Func<List<ElementItemId>, ElementItemId> profileFactory = _ => { profileCalls++; return new ElementItemId(new ElementId(new DocumentId("rev"), 3), new ItemLabel(112, 5, "", "")); };
            Check(SketchTopology.ClosedProfile(new List<ElementItemId>(), false, profileFactory).IsEmpty && profileCalls == 0, "Open path still invokes the failing native profile factory");
            Check(!SketchTopology.ClosedProfile(new List<ElementItemId>(), true, profileFactory).IsEmpty && profileCalls == 1, "Closed profiles lost their native creation path");

            foreach (var scale in new[] { .001, .01, 1.0 })
            foreach (var f in new[] { 12.5, -12.5, 3.0 })
            foreach (var angle in new[] { 0.0, 90.0, -30.0 })
            {
                var parabola = JObject.Parse("{kind:'parabola',vertex:{x:10,y:-20},focalLength:12.5,startParameter:-50,endParameter:30,rotationDegrees:0}");
                parabola["focalLength"] = f; parabola["rotationDegrees"] = angle;
                var plan = SketchCurveGeometry.Parse(parabola, scale);
                Check(plan.Points.Length == 4 && !plan.Closed && plan.SegmentCount == 1, "Parabola must be one open cubic segment, not a sampled polyline");
                var a = angle * Math.PI / 180;
                for (var i = 0; i <= 100; i++)
                {
                    var p = SketchCurveGeometry.Cubic(plan.Points, i / 100.0); var t = -50 + 80 * i / 100.0;
                    // Independent analytic equation in the requested length units.
                    var x = (p.X / scale - 10) * Math.Cos(a) + (p.Y / scale + 20) * Math.Sin(a);
                    var y = -(p.X / scale - 10) * Math.Sin(a) + (p.Y / scale + 20) * Math.Cos(a);
                    Check(Math.Abs(x - t) < 1e-10 && Math.Abs(y - t * t / (4 * f)) < 1e-10, "Cubic controls do not represent the requested parabola");
                }
            }
            var invalidParabola = JObject.Parse("{kind:'parabola',vertex:{x:0,y:0},focalLength:0,startParameter:-10,endParameter:10,rotationDegrees:0}");
            Throws<ArgumentException>(() => SketchCurveGeometry.Parse(invalidParabola, .001));
            invalidParabola["focalLength"] = 5; invalidParabola["startParameter"] = 20;
            Throws<ArgumentException>(() => SketchCurveGeometry.Parse(invalidParabola, .001));
            invalidParabola["startParameter"] = -10; invalidParabola.Remove("rotationDegrees");
            Throws<ArgumentException>(() => SketchCurveGeometry.Parse(invalidParabola, .001));
            Throws<ArgumentException>(() => SketchCurveGeometry.Parse(JObject.Parse("{kind:'arc',start:{x:10,y:0},end:{x:0,y:9},center:{x:0,y:0},clockwise:false}"), .001));
            Check(SketchCurveGeometry.Parse(JObject.Parse("{kind:'arc',start:{x:10,y:0},end:{x:0,y:10},center:{x:0,y:0},clockwise:false}"), .001).Radius == .01, "Arc unit conversion failed");

            var basis = ModelingGeometry.Plane("yz", new Point3D(1, 2, 3));
            var placed = SketchPlacement.Place(basis, new Point3D(.01, .02, .03), 90);
            Check(Math.Abs(placed.Origin.X - 1.03) < 1e-12 && Math.Abs(placed.Origin.Y - 2.01) < 1e-12 && Math.Abs(placed.Origin.Z - 3.02) < 1e-12, "Reference offset was interpreted as world XYZ instead of local XY/normal");
            var absolute = placed.ToAbsolute(new Point2D(.04, .05));
            Check(Math.Abs(absolute.X - 1.03) < 1e-12 && Math.Abs(absolute.Y - 1.96) < 1e-12 && Math.Abs(absolute.Z - 3.06) < 1e-12, "Rotation around the sketch normal is wrong");
            var local = SketchPlacement.ToLocal(placed, absolute);
            Check(Math.Abs(local.X - .04) < 1e-12 && Math.Abs(local.Y - .05) < 1e-12, "Local/world conversion roundtrip failed");
            Throws<ArgumentException>(() => SketchPlacement.ToLocal(placed, new Point3D(absolute.X + .01, absolute.Y, absolute.Z)));
            AutomationGateway.VerifySketchPlacement(new SketchPlacement { Plane = placed }, new SketchPlacement { Plane = placed });
            Throws<InvalidOperationException>(() => AutomationGateway.VerifySketchPlacement(new SketchPlacement { Plane = placed }, new SketchPlacement { Plane = basis }));
            Throws<ArgumentException>(() => SketchPlacement.ValidateFrame(JObject.Parse("{origin:{x:0,y:0,z:0},xDirection:{x:1,y:0,z:0},yDirection:{x:1,y:0,z:0}}"), false));
            Throws<ArgumentException>(() => SketchPlacement.ValidateFrame(JObject.Parse("{origin:{x:0,y:0,z:0},xDirection:{x:1,y:0,z:0},yDirection:{x:0,y:-1,z:0}}"), true));

            using (var gateway = new AutomationGateway())
            {
                var registry = new ToolRegistry(gateway); var catalog = registry.List();
                var definition = (JObject)catalog.Single(t => (string)t["name"] == "topsolid_create_sketches2d")["inputSchema"];
                var batch = JObject.Parse("{documentId:'rev',documentSpace:'3d',units:'mm',sketches:[{placement:'xy',name:'Circle',origin:{x:20,y:30,z:40},rotationDegrees:90,profiles:[{kind:'circle',origin:{x:0,y:0},radius:10}]},{placement:'xz',name:'Parabola',profiles:[{kind:'parabola',vertex:{x:0,y:0},focalLength:12.5,startParameter:-50,endParameter:50,rotationDegrees:0}]}]}");
                Schema.Validate(batch, definition); SketchPlanTools.Validate(batch);
                Check(Throws<RpcException>(() => registry.Call("topsolid_create_sketches2d", batch)).Code == -32010, "Batch creation bypassed confirmation");
                batch["sketches"][1]["sectionMode"] = "combined"; Throws<ArgumentException>(() => SketchPlanTools.Validate(batch)); ((JObject)batch["sketches"][1]).Remove("sectionMode");
                var reference = JObject.Parse("{placement:'reference',referenceSketch:{documentId:'rev',id:7},origin:{x:10,y:0,z:0},anchor:{item:{element:{documentId:'rev',id:7},label:{type:1,id:1}},location:'vertex'},profiles:[{kind:'line',start:{x:0,y:0},end:{x:20,y:0}}]}");
                batch["sketches"] = new JArray(reference); Schema.Validate(batch, definition); SketchPlanTools.Validate(batch);
                var rebased = MutationReferences.Rebase(batch, "rev2");
                Check((string)rebased["sketches"][0]["referenceSketch"]["documentId"] == "rev2" && (string)rebased["sketches"][0]["anchor"]["item"]["element"]["documentId"] == "rev2", "Reference or anchor handle was not rebased with the document");
                Check((string)batch["sketches"][0]["referenceSketch"]["documentId"] == "rev", "Rebase mutated the approved arguments");
                reference["anchor"]["item"]["element"]["id"] = 8; Throws<ArgumentException>(() => SketchPlanTools.Validate(batch)); reference["anchor"]["item"]["element"]["id"] = 7;
                reference["referenceSketch"]["documentId"] = "other"; Throws<ArgumentException>(() => SketchPlanTools.Validate(batch)); reference["referenceSketch"]["documentId"] = "rev";
                ((JArray)batch["sketches"]).Add(JObject.Parse("{sketch:{documentId:'rev',id:7},profiles:[{kind:'circle',origin:{x:0,y:0},radius:10}]}"));
                Throws<ArgumentException>(() => SketchPlanTools.Validate(batch));
                batch["sketches"] = new JArray(Enumerable.Range(0, 2).Select(_ => JObject.Parse("{sketch:{documentId:'rev',id:7},profiles:[{kind:'circle',origin:{x:0,y:0},radius:10}]}")));
                Throws<ArgumentException>(() => SketchPlanTools.Validate(batch));
                batch["sketches"] = new JArray(Enumerable.Range(0, 2).Select(_ => new JObject { ["placement"] = "xy", ["profiles"] = new JArray(Enumerable.Range(0, 17).Select(i => JObject.Parse("{kind:'circle',origin:{x:0,y:0},radius:10}"))) }));
                Throws<ArgumentException>(() => SketchPlanTools.Validate(batch));
                var writeTool = catalog.Single(t => (string)t["name"] == "topsolid_create_sketches2d");
                Check((bool)writeTool["_meta"]["topsolid/requiresConfirmation"] && !(bool)writeTool["annotations"]["readOnlyHint"], "New geometry tool is not marked as a confirmed write");
            }
        }
    }
}
