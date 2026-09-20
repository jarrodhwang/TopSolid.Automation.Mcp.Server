using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed class CylinderPlan
    {
        internal Point3D Origin;
        internal Direction3D Axis, Radial, Tangent;
        internal double Radius, Height;
        internal string Method;
        internal static JObject Vector(double x, double y, double z) => new JObject { ["x"] = x, ["y"] = y, ["z"] = z };
        internal static CylinderPlan Parse(JObject p, double heightSI)
        {
            var scale = ModelingGeometry.Scale(p);
            var axis = SpatialInput.Direction(p["axisDirection"] ?? Vector(0, 0, 1));
            var radial = Math.Abs(axis.Z) < .9 ? SpatialInput.Direction(Vector(-axis.Y, axis.X, 0)) : SpatialInput.Direction(Vector(axis.Z, 0, -axis.X));
            var tangent = new Direction3D(axis.Y * radial.Z - axis.Z * radial.Y, axis.Z * radial.X - axis.X * radial.Z, axis.X * radial.Y - axis.Y * radial.X);
            var plan = new CylinderPlan { Origin = SpatialInput.Point(p["origin"] ?? Vector(0, 0, 0), scale), Axis = axis, Radial = radial, Tangent = tangent,
                Radius = (double)p["diameter"] * scale / 2, Height = heightSI, Method = (string)p["method"] ?? "extrude" };
            if (!(plan.Radius > 0 && plan.Height > 0) || double.IsInfinity(plan.ExpectedVolume)) throw new ArgumentException("Cylinder diameter and height must be finite and positive.");
            if (plan.Method != "extrude" && plan.Method != "revolve") throw new ArgumentException("Cylinder method must be extrude or revolve.");
            if (plan.Method == "revolve" && p["heightParameter"] != null) throw new ArgumentException("Revolved cylinder height is a sketch coordinate; this API does not create driven sketch dimensions. Use an absolute height, or extrude with heightParameter. No snapshot substitution is made.");
            return plan;
        }
        internal double ExpectedVolume => Math.PI * Radius * Radius * Height;
        internal void VerifyVolume(double actual)
        {
            if (!(actual > 0) || double.IsInfinity(actual) || Math.Abs(actual - ExpectedVolume) > Math.Max(1e-12, ExpectedVolume * 1e-6))
                throw new InvalidOperationException("Native solid volume does not match the requested cylinder; rolling back.");
        }
        internal JObject SketchArguments(JObject p)
        {
            var scale = ModelingGeometry.Scale(p); var y = Method == "revolve" ? Axis : Tangent;
            return new JObject { ["documentId"] = p["documentId"], ["documentSpace"] = "3d", ["placement"] = "frame", ["units"] = (string)p["units"] ?? "mm",
                ["frame"] = new JObject { ["origin"] = Vector(Origin.X / scale, Origin.Y / scale, Origin.Z / scale),
                    ["xDirection"] = Vector(Radial.X, Radial.Y, Radial.Z), ["yDirection"] = Vector(y.X, y.Y, y.Z) },
                ["profiles"] = new JArray(Method == "revolve"
                    ? new JObject { ["kind"] = "rectangle", ["origin"] = new JObject { ["x"] = 0, ["y"] = 0 }, ["width"] = Radius / scale, ["height"] = Height / scale }
                    : new JObject { ["kind"] = "circle", ["origin"] = new JObject { ["x"] = 0, ["y"] = 0 }, ["radius"] = Radius / scale }) };
        }
    }

    internal sealed partial class AutomationGateway
    {
        public JObject PreviewCylinder(JObject p)
        {
            var preview = PreviewModeling(p);
            RequireCylinderTarget((string)p["documentId"], (string)preview["type"], TopSolidHost.Documents.EditedDocument.PdmDocumentId);
            var height = FeatureDimension.Length(p, "height", ModelingGeometry.Scale(p)); var plan = CylinderPlan.Parse(p, height.ValueSI);
            preview["method"] = plan.Method; preview["sketchPlan"] = PreviewSketchPlan(plan.SketchArguments(p)); preview["height"] = height.Receipt;
            preview["expectedVolumeCubicMetres"] = plan.ExpectedVolume;
            preview["replaceShapes"] = new JArray(ReplacementShapes(p).Select(id => new JObject { ["shape"] = AutomationValues.Json(id), ["name"] = TopSolidHost.Elements.GetFriendlyName(id) }));
            return preview;
        }
        private ElementId[] ReplacementShapes(JObject p)
        {
            var ids = ((JArray)p["replaceShapes"] ?? new JArray()).Select(handle => Element(new JObject { ["element"] = handle.DeepClone() })).ToArray();
            if (ids.Length == 0) return ids;
            if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("Replacement shape handles must be distinct.");
            var shapes = TopSolidHost.Shapes.GetShapes(Document(p));
            if (ids.Any(id => !shapes.Contains(id) || !TopSolidHost.Elements.IsDeletable(id))) throw new ArgumentException("Each replacement target must be an existing deletable shape in the target document.");
            return ids;
        }
        // A non-edited part produced a native null-reference fault in the supplied
        // session. Keep this workflow on the reviewed active part; do not silently
        // activate a different document during prepare or execution.
        internal static void RequireCylinderTarget(string target, string type, string edited)
        {
            if (string.IsNullOrWhiteSpace(type) || !type.EndsWith(".PartDocument", StringComparison.Ordinal) && type != "TopSolid.Cam.NC.MillTurn.DB.Documents.MillTurnDocument")
                throw new ArgumentException("Cylinder creation requires a design part or CAM modeling document. Select and activate the intended document in TopSolid.");
            if (string.IsNullOrEmpty(target) || target != edited)
                throw new ArgumentException("Activate the selected document in TopSolid before preparing a cylinder. No document was activated and no geometry was created.");
        }
        public JObject CreateCylinder(JObject p)
        {
            EnsureConnected();
            RequireCylinderTarget((string)p["documentId"], TopSolidHost.Documents.GetTypeFullName(Document(p)), TopSolidHost.Documents.EditedDocument.PdmDocumentId);
            return Modify(p, "create cylinder", "kernel", (doc, current) =>
        {
            var height = FeatureDimension.Length(current, "height", ModelingGeometry.Scale(current)); var plan = CylinderPlan.Parse(current, height.ValueSI);
            var replacements = ReplacementShapes(current);
            var sketchResult = CreateSketchProfilesCore(doc, plan.SketchArguments(current));
            var sketch = Element(new JObject { ["sketch"] = sketchResult["sketch"].DeepClone() }, "sketch");
            var section = new SmartSection3D(sketch);
            var shape = plan.Method == "revolve"
                ? TopSolidHost.Shapes.CreateRevolvedShape(doc, section, new SmartAxis3D(new Axis3D(plan.Origin, plan.Axis), -1, 1), null, false, false)
                : TopSolidHost.Shapes.CreateExtrudedShape(doc, section, new SmartDirection3D(plan.Axis, plan.Origin), height.Smart, null, false, false);
            RequireValid(shape, "cylinder"); var volume = TopSolidHost.Shapes.GetShapeVolume(shape); plan.VerifyVolume(volume);
            CreationNames.SetAndVerify(TopSolidHost.Elements, shape, (string)current["name"]);
            var color = current["color"] == null ? null : ElementAppearance.Set(TopSolidHost.Elements, shape, current["color"]);
            // A requested replacement is atomic: preserve the original until the
            // new native geometry and appearance have passed their readbacks.
            if (replacements.Length > 0) {
                TopSolidHost.Elements.DeleteSeveral(replacements.ToList());
                if (replacements.Any(TopSolidHost.Elements.Exists)) throw new InvalidOperationException("A requested old shape was not deleted; rolling back replacement.");
                RequireValid(shape, "replacement cylinder"); RequireValid(sketch, "replacement sketch"); plan.VerifyVolume(TopSolidHost.Shapes.GetShapeVolume(shape));
            }
            return new JObject { ["shape"] = AutomationValues.Json(shape), ["sketch"] = sketchResult["sketch"], ["profiles"] = sketchResult["profiles"],
                ["primitive"] = "cylinder", ["name"] = TopSolidHost.Elements.GetName(shape), ["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(shape),
                ["sketchName"] = sketchResult["name"], ["sketchFriendlyName"] = sketchResult["friendlyName"],
                ["method"] = plan.Method, ["diameterMetres"] = 2 * plan.Radius, ["height"] = height.Receipt,
                ["axisOriginMetres"] = AutomationValues.Json(plan.Origin), ["axisDirection"] = AutomationValues.Json(plan.Axis), ["volumeCubicMetres"] = volume,
                ["color"] = color, ["sectionCreated"] = false, ["geometryReadBack"] = true, ["replacedShapes"] = AutomationValues.Json(replacements),
                ["replacementPolicy"] = "Only the explicitly listed shapes are removed after the new cylinder passes validation, in the same transaction. Old source sketches remain." };
            });
        }
    }
}
