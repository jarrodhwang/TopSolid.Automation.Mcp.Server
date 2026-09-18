using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal static class SpatialInput
    {
        public static Point3D Point(JToken p, double scale) => new Point3D((double)p["x"] * scale, (double)p["y"] * scale, (double)p["z"] * scale);
        public static Direction3D Direction(JToken p)
        {
            double x = (double)p["x"], y = (double)p["y"], z = (double)p["z"];
            var length = Math.Sqrt(x*x + y*y + z*z);
            if (length < 1e-12 || double.IsInfinity(length) || double.IsNaN(length)) throw new ArgumentException("Direction must be a nonzero finite vector.");
            return new Direction3D(x/length, y/length, z/length);
        }
        public static Frame3D Frame(JObject p, double scale)
        {
            var x = Direction(p["xAxis"]); var y = Direction(p["yAxis"]);
            if (Math.Abs(x.X*y.X+x.Y*y.Y+x.Z*y.Z) > 1e-8) throw new ArgumentException("Frame X and Y axes must be perpendicular.");
            return new Frame3D(Point(p["origin"], scale), x, y, new Direction3D(x.Y*y.Z-x.Z*y.Y, x.Z*y.X-x.X*y.Z, x.X*y.Y-x.Y*y.X));
        }
        public static Transform3D Translation(JObject p)
        {
            var point = Point(p["translation"], ModelingGeometry.Scale(p));
            var transform = Transform3D.Identity;
            transform.SetTranslation(new Vector3D(point.X, point.Y, point.Z));
            return transform;
        }
    }

    internal sealed partial class AutomationGateway
    {
        private SmartSection3D Section(JObject arguments)
        {
            if (arguments["sketch"] != null)
            {
                var sketch = Element(arguments, "sketch");
                ValidateShapeSketch(TopSolidHost.Sketches2D, sketch, (bool?)arguments["surface"] ?? false);
                TopSolidHost.Sketches2D.GetPlane(sketch);
                return new SmartSection3D(sketch);
            }
            var section = Item(new JObject { ["item"] = arguments["section"].DeepClone() });
            if (!TopSolidHost.Sketches2D.IsSketch(section.ElementId) || !TopSolidHost.Sketches2D.GetSections(section.ElementId).Contains(section))
                throw new ArgumentException("Use a section handle returned by a native 2D sketch tool.");
            var sectionProfiles = TopSolidHost.Sketches2D.GetSectionProfiles(section);
            if (sectionProfiles.Count == 0 || !((bool?)arguments["surface"] ?? false) && sectionProfiles.Any(profile => !TopSolidHost.Sketches2D.IsProfileClosed(profile)))
                throw new ArgumentException("An existing section must contain native profiles; solid features require all of them to be closed.");
            TopSolidHost.Sketches2D.GetPlane(section.ElementId); // Reject sketches in 2D documents before shape creation.
            return new SmartSection3D(section.ElementId, section.ItemLabel);
        }
        internal static void ValidateShapeSketch(ISketches2D sketches, ElementId sketch, bool surface)
        {
            if (!sketches.IsSketch(sketch)) throw new ArgumentException("A shape input must be a 2D sketch embedded in a 3D document.");
            var profiles = sketches.GetProfiles(sketch);
            if (profiles.Count == 0) throw new ArgumentException("This sketch has no native profile. Separate line entries do not share vertices, even when endpoints have equal coordinates. Create one rectangle, closed polyline or connected contour for a closed boundary; do not guess a section/profile label. Keep existing geometry until a replacement succeeds.");
            if (!surface && profiles.Any(p => !sketches.IsProfileClosed(p))) throw new ArgumentException("A solid feature requires closed native profiles. Open drawing segments remain valid drawings; do not close them without the user's intended boundary.");
        }
        public JObject PreviewShape(string operation, JObject p, bool batch = false)
        {
            var preview = PreviewDocument(p); var inputs = batch ? ((JArray)p["features"]).Cast<JObject>() : new[] { p };
            preview["features"] = new JArray(inputs.Select(input => {
                if (operation == "loft") { ValidateLoft(input); return new JObject { ["profiles"] = input["profiles"].DeepClone() }; }
                Section(input); // Validate topology and 3D placement BEFORE approval.
                var dimension = operation == "extrude" ? FeatureDimension.Length(input, "length", ModelingGeometry.Scale(p)) : FeatureDimension.Angle(input);
                return new JObject { ["sketch"] = input["sketch"]?.DeepClone(), ["section"] = input["section"]?.DeepClone(), ["dimension"] = dimension.Receipt };
            })); return preview;
        }
        private global::System.Collections.Generic.List<ElementItemId> ValidateLoft(JObject p)
        {
            var profiles = ((JArray)p["profiles"]).Select(v => Item(new JObject { ["item"] = v.DeepClone() })).ToList();
            if (profiles.Distinct().Count() != profiles.Count) throw new ArgumentException("Loft profiles must be distinct.");
            foreach (var profile in profiles) {
                if (!TopSolidHost.Sketches2D.IsSketch(profile.ElementId) || !TopSolidHost.Sketches2D.GetProfiles(profile.ElementId).Contains(profile))
                    throw new ArgumentException("Loft currently accepts profiles from 2D sketches embedded in 3D documents.");
                TopSolidHost.Sketches2D.GetPlane(profile.ElementId);
                if (!((bool?)p["surface"] ?? false) && !TopSolidHost.Sketches2D.IsProfileClosed(profile)) throw new ArgumentException("A solid loft requires closed profiles.");
            } return profiles;
        }
        public JObject CreateShape(string operation, JObject arguments)
        {
            return Modify(arguments, operation + " shape", "kernel", (doc, current) =>
            {
                var scale = ModelingGeometry.Scale(current);
                var surface = (bool?)current["surface"] ?? false;
                ElementId shape; FeatureDimension dimension = null;
                if (operation == "loft")
                {
                    var profiles = ValidateLoft(current);
                    shape = TopSolidHost.Shapes.CreateLoftedShape(doc, false, null,
                        profiles.Select(v => new SmartProfile3D(v.ElementId, v.ItemLabel, false)).ToList(), null, null,
                        CurveParametricApproximationType.ArcLength, null, CurveParametricApproximationType.ArcLength,
                        new SmartReal(UnitType.Length, 0.000001), FacesDivisionType.Minimum, false, true, surface);
                }
                else
                {
                    var smartSection = Section(current);
                    var centered = (bool?)current["centered"] ?? false;
                    if (operation == "extrude")
                    {
                        var direction = SpatialInput.Direction(current["direction"]);
                        dimension = FeatureDimension.Length(current, "length", scale);
                        shape = TopSolidHost.Shapes.CreateExtrudedShape(doc, smartSection, new SmartDirection3D(direction, new Point3D(0, 0, 0)),
                            dimension.Smart, null, centered, surface);
                    }
                    else
                    {
                        var axis = new Axis3D(SpatialInput.Point(current["axisOrigin"], scale), SpatialInput.Direction(current["axisDirection"]));
                        dimension = FeatureDimension.Angle(current);
                        shape = TopSolidHost.Shapes.CreateRevolvedShape(doc, smartSection, new SmartAxis3D(axis, -1, 1),
                            dimension.RevolutionAngle, centered, surface);
                    }
                }
                RequireValid(shape, "shape");
                CreationNames.SetAndVerify(TopSolidHost.Elements, shape, (string)current["name"]);
                var result = new JObject { ["shape"] = AutomationValues.Json(shape), ["surface"] = surface, ["geometryReadBack"] = true };
                result["name"] = TopSolidHost.Elements.GetName(shape); result["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(shape);
                if (dimension != null) result["dimension"] = dimension.Receipt;
                if (current["color"] != null) result["color"] = ElementAppearance.Set(TopSolidHost.Elements, shape, current["color"]);
                if (!surface)
                {
                    var volume = TopSolidHost.Shapes.GetShapeVolume(shape);
                    if (!(volume > 0) || double.IsInfinity(volume)) throw new InvalidOperationException("TopSolid did not return a positive solid volume.");
                    result["volumeCubicMetres"] = volume;
                }
                return result;
            });
        }

        public JObject PreviewThroughDrilling(JObject p)
        {
            var preview = PreviewDocument(p, "cad"); var doc = Document(p);
            if (!TopSolidDesignHost.Parts.IsPart(doc)) throw new ArgumentException("Drilling requires a Design part document.");
            if (!TopSolidHost.Shapes.GetShapes(doc).Contains(Element(p, "shape"))) throw new ArgumentException("The drilling target must be an existing shape in this part.");
            preview["diameter"] = FeatureDimension.Length(p, "diameter", ModelingGeometry.Scale(p)).Receipt;
            return preview;
        }
        public JObject CreateThroughDrilling(JObject arguments)
        {
            return Modify(arguments, "through drilling", "cad", (doc, current) =>
            {
                if (!TopSolidDesignHost.Parts.IsPart(doc)) throw new ArgumentException("Drilling requires a Design part document.");
                var shape = Element(current, "shape");
                if (!TopSolidHost.Shapes.GetShapes(doc).Contains(shape)) throw new ArgumentException("The target must be a shape in this part.");
                var scale = ModelingGeometry.Scale(current);
                var frame = SpatialInput.Frame((JObject)current["frame"], scale);
                var before = TopSolidHost.Operations.GetOperations(doc);
                var diameter = FeatureDimension.Length(current, "diameter", scale);
                TopSolidDesignHost.Parts.CreateDrillingOperation(doc, new SmartShape(shape),
                    new SmartFrame3D(frame, -1, 1, -1, 1, -1, 1),
                    new List<DrillingPrimitive> { new DrillingHolePrimitive(diameter.Smart) }, false);
                var created = TopSolidHost.Operations.GetOperations(doc).Except(before).ToList();
                if (created.Count == 0) throw new InvalidOperationException("TopSolid created no drilling operation.");
                foreach (var operation in created) RequireValid(operation, "drilling operation");
                return new JObject { ["operations"] = AutomationValues.Json(created), ["shapes"] = AutomationValues.Json(TopSolidHost.Shapes.GetShapes(doc)), ["diameter"] = diameter.Receipt, ["through"] = true };
            });
        }
    }
}
