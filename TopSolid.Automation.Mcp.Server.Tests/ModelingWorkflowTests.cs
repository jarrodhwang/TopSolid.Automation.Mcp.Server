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
        private static void ModelingWorkflows()
        {
            AutomationGateway.RequireCylinderTarget("part-revision", "TopSolid.Cad.Design.DB.Documents.PartDocument", "part-revision");
            AutomationGateway.RequireCylinderTarget("cam-revision", "TopSolid.Cam.NC.MillTurn.DB.Documents.MillTurnDocument", "cam-revision");
            Throws<ArgumentException>(() => AutomationGateway.RequireCylinderTarget("cam-method", "TopSolid.Cam.NC.MillTurn.DB.Documents.MethodDocument", "cam-method"));
            Throws<ArgumentException>(() => AutomationGateway.RequireCylinderTarget("inactive-part", "TopSolid.Cad.Design.DB.Documents.PartDocument", "other-part"));
            Throws<ArgumentException>(() => AutomationGateway.RequireCylinderTarget("part", null, "part"));
            // Omitted monikers must round-trip as empty strings; absent names stay null.
            foreach (var label in new[] { new ItemLabel(112, 1, "", null), new ItemLabel(112, 2, "p7(2)", null), new ItemLabel(112, 3, "", "Named face") }) {
                var restored = AutomationValues.ParseItemLabel(AutomationValues.Json(label));
                Check(restored.Type == label.Type && restored.Id == label.Id && restored.Moniker == label.Moniker && restored.Name == label.Name, "Native topology label did not survive JSON round-trip");
            }

            // The reported 100 x 350 cylinder must never become a rectangular box.
            var cylinder = JObject.Parse("{documentId:'rev',diameter:100,height:350,color:{r:255,g:0,b:0}}");
            var plan = CylinderPlan.Parse(cylinder, .350);
            Check(Math.Abs(plan.ExpectedVolume - .0027488935718910693) < 1e-15, "Cylinder volume must use radius=diameter/2");
            plan.VerifyVolume(.0027488935718910693);
            Throws<InvalidOperationException>(() => plan.VerifyVolume(.0035)); // box volume in the attached log
            foreach (var method in new[] { "extrude", "revolve" })
            foreach (var units in new[] { "mm", "cm", "m" })
            foreach (var direction in new[] { new[] { 0.0,0.0,1.0 }, new[] { 0.0,0.0,-1.0 }, new[] { 1.0,0.0,0.0 }, new[] { 0.0,1.0,0.0 }, new[] { 1.0,2.0,3.0 } }) {
                var p = (JObject)cylinder.DeepClone(); p["method"] = method; p["units"] = units; var scale = ModelingGeometry.Scale(p);
                p["diameter"] = .1/scale; p["height"] = .35/scale; p["origin"] = CylinderPlan.Vector(12, -7, 3); p["axisDirection"] = CylinderPlan.Vector(direction[0], direction[1], direction[2]);
                var geometry = CylinderPlan.Parse(p, .35); var sketch = geometry.SketchArguments(p); SketchPlanSchema.ValidateProfiles(sketch, scale);
                Check((string)sketch["profiles"][0]["kind"] == (method == "revolve" ? "rectangle" : "circle"), "Cylinder used the wrong profile type");
                var curve = SketchCurveGeometry.Parse((JObject)sketch["profiles"][0], scale);
                Check(curve.Closed && Math.Abs(geometry.ExpectedVolume-plan.ExpectedVolume) < 1e-14, "Units or method changed cylinder dimensions");
                Check(Math.Abs(geometry.Axis.X*geometry.Radial.X+geometry.Axis.Y*geometry.Radial.Y+geometry.Axis.Z*geometry.Radial.Z) < 1e-12, "Cylinder profile radius is not perpendicular to its axis");
                if (method == "revolve") {
                    Check(Math.Abs(curve.Points[2].X-.05) < 1e-12 && Math.Abs(curve.Points[2].Y-.35) < 1e-12, "Revolution rectangle must use radius and axial height");
                    Check(JToken.DeepEquals(sketch["frame"]["yDirection"], CylinderPlan.Vector(geometry.Axis.X, geometry.Axis.Y, geometry.Axis.Z)), "Revolve local Y must follow world axis");
                }
            }
            var zeroAxis = (JObject)cylinder.DeepClone(); zeroAxis["axisDirection"] = CylinderPlan.Vector(0,0,0); Throws<ArgumentException>(() => CylinderPlan.Parse(zeroAxis, .35));

            // Closed slot analytic geometry: overall length includes its round ends.
            foreach (var degrees in new[] { 0.0, 90.0, -37.0 }) {
                var slot = SketchCurveGeometry.Parse(JObject.Parse("{kind:'slot',center:{x:12,y:-7},length:100,width:20,rotationDegrees:" + degrees.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + "}"), .001);
                Check(slot.Closed && slot.SegmentCount == 4 && slot.Radius == .01, "Slot requires a connected four-segment analytic profile");
                Check(Math.Abs(SketchCurveGeometry.Distance(slot.SlotCenters[0], slot.SlotCenters[1]) - .08) < 1e-12, "Slot length was mistaken for center spacing");
                foreach (var i in new[] { 1, 3 }) {
                    Check(Math.Abs(SketchCurveGeometry.Distance(slot.Points[i], slot.SlotCenters[i/2]) - .01) < 1e-12, "Slot arc start radius differs");
                    Check(Math.Abs(SketchCurveGeometry.Distance(slot.Points[(i+1)%4], slot.SlotCenters[i/2]) - .01) < 1e-12, "Slot arc end radius differs");
                }
            }
            Throws<ArgumentException>(() => SketchCurveGeometry.Parse(JObject.Parse("{kind:'slot',center:{x:0,y:0},length:10,width:20,rotationDegrees:0}"), .001));
            Throws<ArgumentException>(() => SketchCurveGeometry.Parse(JObject.Parse("{kind:'circle',center:{x:0,y:0},radius:10}"), .001));

            var id = new ElementId(new DocumentId("rev"), 7); var profileId = new ElementItemId(id, new ItemLabel(112, 1, "p7(1)", null));
            var nativeProfiles = new List<ElementItemId>(); var closed = false;
            var sketchApi = new ContractProxy<ISketches2D>((call,args) => call.MethodName == "IsSketch" ? (object)true : call.MethodName == "GetProfiles" ? nativeProfiles : call.MethodName == "IsProfileClosed" ? closed : throw new Exception("Unexpected call " + call.MethodName));
            Check(Throws<ArgumentException>(() => AutomationGateway.ValidateShapeSketch(sketchApi.Interface, id, false)).Message.Contains("Separate line"), "Missing-profile error must explain the actual topology problem");
            nativeProfiles.Add(profileId); Throws<ArgumentException>(() => AutomationGateway.ValidateShapeSketch(sketchApi.Interface, id, false));
            AutomationGateway.ValidateShapeSketch(sketchApi.Interface, id, true); closed = true; AutomationGateway.ValidateShapeSketch(sketchApi.Interface, id, false);
            Check(sketchApi.Calls.All(n => !n.StartsWith("Create") && !n.StartsWith("Start")), "Profile validation must remain read-only before approval");

            // Native dependencies must survive even when an angle currently equals 360 degrees.
            var parameterUnit = UnitType.Length; var parameterValue = .35; var parameterType = ParameterType.Real; var valid = true;
            var parameters = new ContractProxy<IParameters>((call,args) => {
                if (call.MethodName == "GetParameterType") return parameterType;
                if (call.MethodName == "HasValue") return true;
                if (call.MethodName == "GetRealUnit") { args[1] = parameterUnit; args[2] = parameterUnit == UnitType.Length ? "mm" : "deg"; return null; }
                if (call.MethodName == "GetRealValue") return parameterValue;
                throw new Exception("Unexpected parameter call " + call.MethodName);
            });
            var currentColor = new Color(1,2,3); var editable = true; var ignoreColor = false;
            var elements = new ContractProxy<IElements>((call,args) => {
                switch (call.MethodName) {
                    case "Exists": return valid; case "IsInvalid": return !valid; case "GetName": return "Height";
                    case "IsColorModifiable": return editable; case "HasColor": return true; case "GetColor": return currentColor;
                    case "SetColor": if (!ignoreColor) currentColor = (Color)args[1]; return null;
                    default: throw new Exception("Unexpected element call " + call.MethodName);
                }
            });
            var parameterInput = new JObject { ["lengthParameter"] = AutomationValues.Json(id) };
            FeatureDimension Resolve() => FeatureDimension.Resolve(parameterInput, "length", "lengthParameter", UnitType.Length, .001, parameters.Interface, elements.Interface);
            var dimension = Resolve(); Check(dimension.Smart.Type == SmartRealType.Element && dimension.Smart.ElementId.Equals(id) && dimension.Smart.Value == null, "Parameter was flattened to its current numeric value");
            parameterValue = .5; Check(Resolve().ValueSI == .5 && Resolve().Smart.ElementId.Equals(id), "Preview must re-read the parameter without changing its binding");
            parameterUnit = UnitType.Angle; Throws<ArgumentException>(() => Resolve()); parameterUnit = UnitType.Length;
            parameterType = ParameterType.Integer; Throws<ArgumentException>(() => Resolve()); parameterType = ParameterType.Real;
            foreach (var invalid in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity }) { parameterValue = invalid; Throws<ArgumentException>(() => Resolve()); }
            parameterValue = .35; valid = false; Throws<ArgumentException>(() => Resolve()); valid = true;
            parameterInput["length"] = 350; Throws<ArgumentException>(() => Resolve()); parameterInput.Remove("lengthParameter");
            dimension = Resolve(); Check(dimension.Smart.Type == SmartRealType.Basic && Math.Abs(dimension.ValueSI-.35) < 1e-12, "Absolute modeling changed default units or created a parameter");
            parameterUnit = UnitType.Angle; parameterValue = 2*Math.PI;
            var angle = FeatureDimension.Resolve(new JObject { ["angleParameter"] = AutomationValues.Json(id) }, "angleDegrees", "angleParameter", UnitType.Angle, Math.PI/180, parameters.Interface, elements.Interface, 2*Math.PI);
            Check(angle.RevolutionAngle != null && angle.RevolutionAngle.Type == SmartRealType.Element, "Full-turn parameter binding must not become constant null");
            var absoluteAngle = FeatureDimension.Resolve(new JObject { ["angleDegrees"] = 360 }, "angleDegrees", "angleParameter", UnitType.Angle, Math.PI/180, parameters.Interface, elements.Interface, 2*Math.PI);
            Check(absoluteAngle.RevolutionAngle == null, "Absolute full turn must follow the documented null convention");
            parameterValue = 3*Math.PI; Throws<ArgumentException>(() => FeatureDimension.Resolve(new JObject { ["angleParameter"] = AutomationValues.Json(id) }, "angleDegrees", "angleParameter", UnitType.Angle, Math.PI/180, parameters.Interface, elements.Interface, 2*Math.PI));

            var red = JObject.Parse("{r:255,g:0,b:0}"); var applied = ElementAppearance.Set(elements.Interface, id, red);
            Check(ElementAppearance.Parse(JObject.Parse("{name:'red'}")).Equals(new Color(255,0,0)), "Named red must not become magenta");
            Check(ElementAppearance.Parse(JObject.Parse("{name:'blue'}")).Equals(new Color(0,0,255)), "Named blue changed");
            foreach (var invalid in new[] { "{}", "{name:'red',r:255,g:0,b:0}", "{name:'unknown'}", "{r:256,g:0,b:0}", "{r:1,b:0}" }) Throws<ArgumentException>(() => ElementAppearance.Parse(JObject.Parse(invalid)));
            Check((bool)applied["readBackVerified"] && currentColor.Equals(new Color(255,0,0)), "Color must modify the element, not create a parameter");
            editable = false; var before = elements.Calls.Count(n => n == "SetColor"); Throws<ArgumentException>(() => ElementAppearance.Set(elements.Interface, id, red));
            Check(elements.Calls.Count(n => n == "SetColor") == before, "Unsupported color setter executed"); editable = true; ignoreColor = true;
            Throws<InvalidOperationException>(() => ElementAppearance.Set(elements.Interface, id, JObject.Parse("{r:0,g:0,b:255}")));

            // Arc plane/direction and open/closed topology are validated in world coordinates.
            var arcInput = JObject.Parse("{kind:'arc',start:{x:10,y:0,z:3},end:{x:0,y:10,z:3},center:{x:0,y:0,z:3},normal:{x:0,y:0,z:1}}");
            var arc = Sketch3DCurve.Parse(arcInput, .001); var mid = arc.ArcMidpoint();
            Check(Math.Abs(mid.X-Math.Sqrt(.00005)) < 1e-12 && Math.Abs(mid.Y-Math.Sqrt(.00005)) < 1e-12 && mid.Z == .003, "3D arc midpoint ignores normal or center plane");
            arcInput["normal"]["z"] = -1; Check(Sketch3DCurve.Parse(arcInput, .001).ArcMidpoint().X < 0, "Opposite arc normal must choose the other sweep");
            arcInput["end"]["z"] = 4; Throws<ArgumentException>(() => Sketch3DCurve.Parse(arcInput, .001));
            var line = Sketch3DCurve.Parse(JObject.Parse("{kind:'polyline',points:[{x:0,y:0,z:0},{x:1,y:2,z:3}],closed:false}"), .001);
            Check(!line.Closed && line.SegmentCount == 1, "Open 3D path must remain open");

            using (var gateway = new AutomationGateway()) {
                var registry = new ToolRegistry(gateway); var catalog = registry.List();
                foreach (var pair in new[] {
                    Tuple.Create("topsolid_create_cylinder", cylinder),
                    Tuple.Create("topsolid_set_entity_colors", new JObject { ["documentId"] = "rev", ["elements"] = new JArray(AutomationValues.Json(id)), ["color"] = red }),
                    Tuple.Create("topsolid_color_shape_faces", new JObject { ["documentId"] = "rev", ["faces"] = new JArray(AutomationValues.Json(profileId)), ["color"] = red }),
                    Tuple.Create("topsolid_create_sketch3d_curves", JObject.Parse("{documentId:'rev',curves:[{kind:'circle',center:{x:0,y:0,z:0},normal:{x:0,y:0,z:1},radius:5}]}")),
                    Tuple.Create("topsolid_extrude_sketch", new JObject { ["documentId"] = "rev", ["sketch"] = AutomationValues.Json(id), ["direction"] = CylinderPlan.Vector(0,0,1), ["lengthParameter"] = AutomationValues.Json(id) }) }) {
                    Schema.Validate(pair.Item2, (JObject)catalog.Single(t => (string)t["name"] == pair.Item1)["inputSchema"]);
                    Check(Throws<RpcException>(() => registry.Call(pair.Item1, pair.Item2)).Code == -32010, "New modeling workflow bypassed approval: " + pair.Item1);
                }
                var both = (JObject)cylinder.DeepClone(); both["heightParameter"] = AutomationValues.Json(id);
                Check(Throws<RpcException>(() => registry.Call("topsolid_create_cylinder", both)).Code == -32602, "Ambiguous dimension not rejected");
                both.Remove("height"); both["method"] = "revolve";
                Check(Throws<RpcException>(() => registry.Call("topsolid_create_cylinder", both)).Message.Contains("driven sketch"), "Unsupported driven sketch dimension silently copied");
                var guide = registry.Call("topsolid_get_modeling_guide", new JObject { ["topic"] = "limitations" });
                Check(!(bool)guide["isError"] && guide.ToString().Contains("Boolean"), "Local guide must distinguish unsupported operations without connecting TopSolid");
            }
        }
    }
}
