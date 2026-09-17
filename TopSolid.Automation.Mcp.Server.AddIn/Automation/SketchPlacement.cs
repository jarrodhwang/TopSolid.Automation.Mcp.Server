using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed class SketchPlacement
    {
        public Plane3D Plane;
        public bool In2D;
        public JObject Reference;
        public SketchAssociativeReference Associative;
        public JObject Json() => new JObject { ["documentSpace"] = In2D ? "2d" : "3d", ["originMetres"] = AutomationValues.Json(Plane.Origin),
            ["xDirection"] = AutomationValues.Json(Plane.XDirection), ["yDirection"] = AutomationValues.Json(Plane.YDirection),
            ["reference"] = Reference, ["associativeInputs"] = Associative?.Json(),
            ["referenceBehavior"] = Reference == null ? "explicit frame" : Associative == null ? "snapshot only; no associative link" : "native links to the reference support plane, anchor vertex and sketch axes; local shape dimensions remain as specified" };

        public static void Validate(JObject p)
        {
            var append = p["sketch"] != null; var placement = (string)p["placement"];
            if (append)
            {
                if (new[] { "placement", "origin", "rotationDegrees", "frame", "referenceSketch", "referenceMode", "anchor", "name" }.Any(k => p[k] != null))
                    throw new ArgumentException("Appending uses the existing sketch frame. Omit name and all placement/reference options.");
                return;
            }
            if (placement == null) throw new ArgumentException("Supply an existing sketch OR an explicit new placement.");
            if ((placement == "frame" || placement == "reference") && p["documentSpace"] == null)
                throw new ArgumentException("For frame/reference placement specify documentSpace=2d or 3d; a 2D sketch may be in either kind of document.");
            if (placement != "reference" && (p["referenceSketch"] != null || p["referenceMode"] != null || p["anchor"] != null)) throw new ArgumentException("Reference handles/mode require placement=reference.");
            if (placement == "reference" && p["referenceSketch"] == null) throw new ArgumentException("Choose an exact referenceSketch from the context tool. Do not guess its name or ID.");
            if (p["anchor"] != null && !JToken.DeepEquals(p["anchor"]["item"]["element"], p["referenceSketch"]))
                throw new ArgumentException("The anchor item must belong to the exact reference sketch.");
            if ((placement == "frame") != (p["frame"] != null)) throw new ArgumentException("placement=frame requires frame; other placements must omit it.");
            if (placement == "frame" && p["origin"] != null) throw new ArgumentException("A custom frame already specifies its origin. Omit origin.");
            var in2D = placement == "2d" || (string)p["documentSpace"] == "2d";
            if (p["documentSpace"] != null && (placement == "2d" && !((string)p["documentSpace"] == "2d") || new[] { "xy", "xz", "yz" }.Contains(placement) && in2D))
                throw new ArgumentException("Principal 3D planes require documentSpace=3d; placement=2d requires a 2D document.");
            if (in2D && p["origin"] != null && (double)p["origin"]["z"] != 0) throw new ArgumentException("A 2D document cannot have a Z origin/normal offset.");
            if (placement == "reference" && (string)p["referenceMode"] != "snapshot")
            {
                if (p["anchor"] == null) throw new ArgumentException("Associative placement requires an explicit reference anchor (vertex, segment endpoint or circle center). Read the reference geometry and ask which point to follow; never substitute a fixed point.");
                if (((double?)p["origin"]?["z"] ?? 0) != 0) throw new ArgumentException("Associative placement currently supports the reference support plane with zero normal offset. An offset support plane needs a separate native operation; do not silently use a snapshot.");
                if (Math.Abs(((double?)p["rotationDegrees"] ?? 0) / 90 - Math.Round(((double?)p["rotationDegrees"] ?? 0) / 90)) > 1e-10)
                    throw new ArgumentException("Associative sketch frame rotation supports multiples of 90 degrees with the installed SDK. Arbitrary linked frame rotation is not implemented; no fixed direction will be substituted.");
                if (in2D && (((double?)p["origin"]?["x"] ?? 0) != 0 || ((double?)p["origin"]?["y"] ?? 0) != 0))
                    throw new ArgumentException("Native 2D documents require zero offset for associative placement. XY offsets are supported for 2D sketches in 3D documents.");
            }
            if (placement == "frame") ValidateFrame((JObject)p["frame"], in2D);
        }
        internal static void ValidateFrame(JObject frame, bool in2D)
        {
            var x = Point(frame["xDirection"], 1); var y = Point(frame["yDirection"], 1);
            var lx = Norm(x); var ly = Norm(y);
            if (Math.Abs(lx - 1) > 1e-8 || Math.Abs(ly - 1) > 1e-8 || Math.Abs(Dot(x, y)) > 1e-8)
                throw new ArgumentException("Frame directions must be orthogonal unit vectors. The server does not silently change orientation.");
            if (in2D && (Math.Abs(x.Z) > 1e-10 || Math.Abs(y.Z) > 1e-10 || (double)frame["origin"]["z"] != 0 || x.X * y.Y - x.Y * y.X < 0.99999999))
                throw new ArgumentException("A 2D frame must be in XY with positive orientation; mirrored frames are not supported.");
        }
        internal static Plane3D Place(Plane3D basis, Point3D localOffset, double degrees)
        {
            var x = basis.XDirection; var y = basis.YDirection; var n = basis.Normal; var o = basis.Origin;
            var angle = degrees * Math.PI / 180; var c = Math.Cos(angle); var s = Math.Sin(angle);
            return new Plane3D(new Point3D(o.X + localOffset.X * x.X + localOffset.Y * y.X + localOffset.Z * n.X,
                o.Y + localOffset.X * x.Y + localOffset.Y * y.Y + localOffset.Z * n.Y,
                o.Z + localOffset.X * x.Z + localOffset.Y * y.Z + localOffset.Z * n.Z),
                new Direction3D(c * x.X + s * y.X, c * x.Y + s * y.Y, c * x.Z + s * y.Z),
                new Direction3D(-s * x.X + c * y.X, -s * x.Y + c * y.Y, -s * x.Z + c * y.Z));
        }
        internal static Point3D Point(JToken p, double scale) => p == null ? new Point3D(0, 0, 0) : new Point3D((double)p["x"] * scale, (double)p["y"] * scale, (double)p["z"] * scale);
        internal static double Dot(Point3D a, Point3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static double Norm(Point3D p) => Math.Sqrt(Dot(p, p));
        internal static Point2D ToLocal(Plane3D plane, Point3D world)
        {
            var o = plane.Origin; var x = plane.XDirection; var y = plane.YDirection; var n = plane.Normal;
            var d = new Point3D(world.X - o.X, world.Y - o.Y, world.Z - o.Z);
            var offPlane = d.X * n.X + d.Y * n.Y + d.Z * n.Z;
            if (Math.Abs(offPlane) > 1e-8) throw new ArgumentException("Reference point is not on the target sketch plane. No projection was requested or performed.");
            return new Point2D(d.X * x.X + d.Y * x.Y + d.Z * x.Z, d.X * y.X + d.Y * y.Y + d.Z * y.Z);
        }
    }

    internal sealed partial class AutomationGateway
    {
        internal SketchPlacement ReadSketchPlacement(ElementId sketch, bool in2D)
        {
            if (!TopSolidHost.Sketches2D.IsSketch(sketch)) throw new ArgumentException("The selected element is not a native 2D sketch.");
            if (!in2D) return new SketchPlacement { In2D = false, Plane = TopSolidHost.Sketches2D.GetPlane(sketch) };
            var f = TopSolidHost.Sketches2D.GetFrame(sketch);
            return new SketchPlacement { In2D = true, Plane = new Plane3D(new Point3D(f.Origin.X, f.Origin.Y, 0),
                new Direction3D(f.XDirection.X, f.XDirection.Y, 0), new Direction3D(f.YDirection.X, f.YDirection.Y, 0)) };
        }
        internal SketchPlacement ResolveSketchPlacement(JObject p, double scale)
        {
            SketchPlacement.Validate(p);
            var placement = (string)p["placement"]; var in2D = placement == "2d" || (string)p["documentSpace"] == "2d";
            if (p["sketch"] != null) return ReadSketchPlacement(Element(p, "sketch"), in2D);
            var result = new SketchPlacement { In2D = in2D }; var offset = SketchPlacement.Point(p["origin"], scale);
            if (placement == "reference")
            {
                var sketch = Element(p, "referenceSketch"); result = ReadSketchPlacement(sketch, in2D);
                result.Reference = new JObject { ["sketch"] = AutomationValues.Json(sketch), ["name"] = TopSolidHost.Elements.GetFriendlyName(sketch), ["internalName"] = TopSolidHost.Elements.GetName(sketch),
                    ["frame"] = result.Json() };
                if (p["anchor"] is JObject anchor)
                {
                    var local = ReadSketchAnchor(sketch, anchor);
                    offset = new Point3D(offset.X + local.X, offset.Y + local.Y, offset.Z);
                    result.Reference["anchor"] = anchor.DeepClone(); result.Reference["anchorPointMetres"] = AutomationValues.Json(local);
                }
                result.Plane = SketchPlacement.Place(result.Plane, offset, (double?)p["rotationDegrees"] ?? 0);
                if ((string)p["referenceMode"] != "snapshot") result.Associative = ResolveAssociativeSketchReference(sketch, (JObject)p["anchor"], SketchPlacement.Point(p["origin"], scale), (double?)p["rotationDegrees"] ?? 0);
            }
            else if (placement == "frame")
            {
                var f = p["frame"]; var x = SketchPlacement.Point(f["xDirection"], 1); var y = SketchPlacement.Point(f["yDirection"], 1);
                result.Plane = SketchPlacement.Place(new Plane3D(SketchPlacement.Point(f["origin"], scale), new Direction3D(x.X, x.Y, x.Z), new Direction3D(y.X, y.Y, y.Z)),
                    new Point3D(0, 0, 0), (double?)p["rotationDegrees"] ?? 0);
            }
            else
            {
                // Principal placements specify a WORLD origin. Reference offsets instead use reference axes.
                result.Plane = SketchPlacement.Place(ModelingGeometry.Plane(placement == "2d" ? "xy" : placement, offset),
                    new Point3D(0, 0, 0), (double?)p["rotationDegrees"] ?? 0);
            }
            return result;
        }
        internal Point2D ReadSketchAnchor(ElementId sketch, JObject anchor)
        {
            var item = Item(anchor); var location = (string)anchor["location"];
            if (!item.ElementId.Equals(sketch)) throw new ArgumentException("Anchor does not belong to the reference sketch.");
            if (location == "vertex")
            {
                if (!TopSolidHost.Sketches2D.GetVertices(sketch).Contains(item)) throw new ArgumentException("The reference vertex is stale or missing.");
                return TopSolidHost.Sketches2D.GetVertexPoint(item);
            }
            if (!TopSolidHost.Sketches2D.GetSegments(sketch).Contains(item)) throw new ArgumentException("The reference segment is stale or missing.");
            if (location == "center")
            {
                if (TopSolidHost.Sketches2D.GetSegmentCurveType(item) != CurveType.Circle) throw new ArgumentException("A center anchor currently requires a circular segment; choose an explicit vertex for other curves.");
                TopSolidHost.Sketches2D.GetSegmentCircleCurve(item, out var frame, out _); return frame.Origin;
            }
            TopSolidHost.Sketches2D.GetSegmentRange(item, out var t0, out var t1);
            if (double.IsInfinity(t0) || double.IsInfinity(t1) || double.IsNaN(t0) || double.IsNaN(t1)) throw new ArgumentException("An unbounded reference segment has no finite start/end/midpoint; choose a vertex or center.");
            var reversed = TopSolidHost.Sketches2D.IsSegmentReversed(item);
            return TopSolidHost.Sketches2D.GetSegmentPoint(item, location == "midParameter" ? (t0 + t1) / 2 : (location == "start") != reversed ? t0 : t1);
        }
        internal ElementId CreatePlacedSketch(DocumentId doc, SketchPlacement p)
        {
            if (p.Associative != null) return CreateAssociativeSketch(doc, p);
            var o = p.Plane.Origin; var x = p.Plane.XDirection;
            return p.In2D ? TopSolidHost.Sketches2D.CreateSketchIn2D(doc, new SmartPoint2D(new Point2D(o.X, o.Y)), true,
                new SmartDirection2D(new Direction2D(x.X, x.Y), new Point2D(o.X, o.Y)))
                : TopSolidHost.Sketches2D.CreateSketchIn3D(doc, new SmartPlane3D(p.Plane, -1, 1, -1, 1), new SmartPoint3D(o), true, new SmartDirection3D(x, o));
        }
    }
}
