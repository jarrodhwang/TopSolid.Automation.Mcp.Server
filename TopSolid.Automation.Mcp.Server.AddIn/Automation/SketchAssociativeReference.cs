using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed class SketchAssociativeReference
    {
        public ElementId Sketch;
        public ElementItemId Anchor, XAxis, YAxis;
        public bool ReverseX, ReverseY;
        public Point3D Offset;
        public double RotationDegrees;
        public readonly List<ElementId> Helpers = new List<ElementId>();
        // The installed 7.20.400.107 (ElementId, ItemLabel) overload reports Type=Element.
        // Use the documented full constructor so the native vertex item is explicit.
        internal SmartPoint3D Origin3D() => new SmartPoint3D(SmartPoint3DType.Item, null, Anchor.ElementId, Anchor.ItemLabel);
        internal SmartDirection3D Direction3D(bool y) => new SmartDirection3D(Sketch, (y ? YAxis : XAxis).ItemLabel, y ? ReverseY : ReverseX);
        public JObject Json() => new JObject { ["supportSketch"] = AutomationValues.Json(Sketch), ["anchorVertex"] = AutomationValues.Json(Anchor),
            ["xAxis"] = AutomationValues.Json(XAxis), ["yAxis"] = AutomationValues.Json(YAxis), ["reverseX"] = ReverseX, ["reverseY"] = ReverseY,
            ["offsetMetres"] = AutomationValues.Json(Offset), ["rotationDegrees"] = RotationDegrees,
            ["helperPolicy"] = "3D documents: create hidden native offset points as needed, in the same confirmed transaction. Native 2D: direct point/direction links.",
            ["helperElements"] = AutomationValues.Json(Helpers) };
    }
    internal sealed partial class AutomationGateway
    {
        internal SketchAssociativeReference ResolveAssociativeSketchReference(ElementId sketch, JObject anchor, Point3D offset, double rotation)
        {
            var source = Item(anchor); var where = (string)anchor["location"]; var vertex = ElementItemId.Empty;
            if (where == "vertex") vertex = source;
            else if (where == "center") vertex = TopSolidHost.Sketches2D.GetSegmentCenter(source);
            else if (where == "midParameter" && TopSolidHost.Sketches2D.GetSegmentCurveType(source) == CurveType.Line)
                vertex = TopSolidHost.Sketches2D.GetSegmentMiddle(source);
            else if (where == "start" || where == "end")
            {
                TopSolidHost.Sketches2D.GetSegmentVertices(source, out var start, out var end); vertex = where == "start" ? start : end;
            }
            if (vertex.IsEmpty || !vertex.ElementId.Equals(sketch) || !TopSolidHost.Sketches2D.GetVertices(sketch).Contains(vertex))
                throw new ArgumentException("This location has no native anchor vertex to link. Select an existing vertex/endpoint/center. A sampled coordinate cannot create an associative point.");
            var axes = ReadSketchReferenceAxes(sketch);
            return new SketchAssociativeReference { Sketch = sketch, Anchor = vertex, XAxis = axes.Item1, YAxis = axes.Item2,
                ReverseX = axes.Item3, ReverseY = axes.Item4, Offset = offset, RotationDegrees = rotation };
        }
        // Use returned topology, never synthesized labels. Native axis names are
        // checked against their analytic local direction and infinite extent.
        private static Tuple<ElementItemId, ElementItemId, bool, bool> ReadSketchReferenceAxes(ElementId sketch)
        {
            var x = ElementItemId.Empty; var y = ElementItemId.Empty; var reverseX = false; var reverseY = false;
            foreach (var item in TopSolidHost.Sketches2D.GetSegments(sketch))
            {
                var name = item.ItemLabel.Name ?? "";
                var isX = name == "$TopSolid.Kernel.G.D2.Sketches.SketchItemName.XAxis";
                var isY = name == "$TopSolid.Kernel.G.D2.Sketches.SketchItemName.YAxis";
                if ((!isX && !isY) || TopSolidHost.Sketches2D.GetSegmentCurveType(item) != CurveType.Line) continue;
                TopSolidHost.Sketches2D.GetSegmentLineCurve(item, out var axis);
                TopSolidHost.Sketches2D.GetSegmentRange(item, out var min, out var max);
                if (!double.IsNegativeInfinity(min) || !double.IsPositiveInfinity(max) || Math.Abs(axis.Origin.X) + Math.Abs(axis.Origin.Y) > 1e-9) continue;
                if (isX && Math.Abs(axis.Direction.Y) < 1e-9 && Math.Abs(Math.Abs(axis.Direction.X) - 1) < 1e-9)
                { if (!x.IsEmpty) throw new ArgumentException("Ambiguous native sketch X axis."); x = item; reverseX = axis.Direction.X < 0; }
                if (isY && Math.Abs(axis.Direction.X) < 1e-9 && Math.Abs(Math.Abs(axis.Direction.Y) - 1) < 1e-9)
                { if (!y.IsEmpty) throw new ArgumentException("Ambiguous native sketch Y axis."); y = item; reverseY = axis.Direction.Y < 0; }
            }
            if (x.IsEmpty || y.IsEmpty) throw new ArgumentException("Could not verify the reference sketch's native axes. Associative placement is unavailable for this sketch; no snapshot substitution was made.");
            return Tuple.Create(x, y, reverseX, reverseY);
        }
        private ElementId CreateAssociativeSketch(DocumentId doc, SketchPlacement placement)
        {
            var link = placement.Associative;
            if (placement.In2D)
            {
                var quarter = ((int)Math.Round(link.RotationDegrees / 90) % 4 + 4) % 4;
                var y = quarter % 2 == 1; var axis = y ? link.YAxis : link.XAxis;
                return TopSolidHost.Sketches2D.CreateSketchIn2D(doc, new SmartPoint2D(SmartPoint2DType.Item, null, link.Anchor.ElementId, link.Anchor.ItemLabel), true,
                    new SmartDirection2D(link.Sketch, axis.ItemLabel, (y ? link.ReverseY : link.ReverseX) ^ (quarter >= 2)));
            }
            SmartPoint3D Offset(SmartPoint3D origin, bool y, double distance)
            {
                if (Math.Abs(distance) < 1e-12) return origin;
                var direction = link.Direction3D(y); var length = new SmartReal(UnitType.Length, distance);
                var point = TopSolidHost.Geometries3D.CreateOffsetPoint(doc, origin, direction, length);
                RequireValid(point, "associative offset point");
                var operation = TopSolidHost.Elements.GetParent(point);
                if (operation.IsEmpty) throw new InvalidOperationException("Offset point has no native parent operation; refusing a detached reference.");
                TopSolidHost.Geometries3D.GetOffsetPointCreation(operation, out var actualOrigin, out var actualDirection, out var actualLength);
                if (actualOrigin.Type != origin.Type || !actualOrigin.ElementId.Equals(origin.ElementId) || !actualOrigin.ItemLabel.Equals(origin.ItemLabel)
                    || actualDirection.Type != direction.Type || !actualDirection.ElementId.Equals(direction.ElementId) || !actualDirection.ItemLabel.Equals(direction.ItemLabel)
                    || actualDirection.IsReversed != direction.IsReversed || !actualLength.Value.HasValue || Math.Abs(actualLength.Value.Value - distance) > 1e-10)
                    throw new InvalidOperationException("Native offset operation did not retain its associative inputs; rolling back.");
                link.Helpers.Add(point); TopSolidHost.Elements.Hide(point);
                return new SmartPoint3D(point);
            }
            var originPoint = Offset(Offset(link.Origin3D(), false, link.Offset.X), true, link.Offset.Y);
            var angle = link.RotationDegrees * Math.PI / 180;
            SmartDirection3D xDirection;
            if (Math.Abs(Math.Sin(angle)) < 1e-12)
                xDirection = new SmartDirection3D(link.Sketch, link.XAxis.ItemLabel, link.ReverseX ^ (Math.Cos(angle) < 0));
            else if (Math.Abs(Math.Cos(angle)) < 1e-12)
                xDirection = new SmartDirection3D(link.Sketch, link.YAxis.ItemLabel, link.ReverseY ^ (Math.Sin(angle) < 0));
            else
            {
                throw new ArgumentException("Associative frame rotation requires a multiple of 90 degrees in this SDK.");
            }
            return TopSolidHost.Sketches2D.CreateSketchIn3D(doc, new SmartPlane3D(link.Sketch, false), originPoint, true, xDirection);
        }
    }
}
