using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed class CamColorGeometry
    {
        private readonly AutomationGateway gateway;
        private readonly Dictionary<ElementId,string> kinds = new Dictionary<ElementId,string>();
        private readonly Dictionary<ElementId,List<ElementItemId>> faces = new Dictionary<ElementId,List<ElementItemId>>();
        internal List<JObject> Targets { get; } = new List<JObject>();
        internal JArray Workpieces { get; } = new JArray();
        internal bool NeedsWorkpiece { get; }
        internal DocumentId Document { get; }
        internal CamColorGeometry(AutomationGateway gateway,JObject input, bool allCamGeometry = false)
        {
            this.gateway = gateway; Document = gateway.Document(input);
            HashSet<ElementId> scope = null;
            if (TopSolidHost.Documents.GetTypeFullName(Document).StartsWith("TopSolid.Cam.",StringComparison.Ordinal))
            {
                gateway.ConnectModule("cam");
                var parts = TopSolidCamHost.Documents.GetParts(Document);
                Workpieces = new JArray(parts.Select(id => new JObject { ["element"] = AutomationValues.Json(id), ["name"] = TopSolidHost.Elements.GetFriendlyName(id) }));
                if (!allCamGeometry) {
                    if (input["workpiece"] == null) { NeedsWorkpiece = true; return; }
                    var selected = gateway.Element(input,"workpiece");
                    if (!parts.Contains(selected)) throw new ArgumentException("Workpiece is not part of the exact CAM document.");
                    scope = CamWorkpieceScope(selected);
                }
            }
            else if (input["workpiece"] != null) scope = Descendants(gateway.Element(input,"workpiece"));

            Add(TopSolidHost.Shapes.GetShapes(Document),"shape",scope);
            Add(TopSolidHost.Sketches2D.GetSketches(Document),"sketch2d",scope); Add(TopSolidHost.Sketches3D.GetSketches(Document),"sketch3d",scope);
            Add(TopSolidHost.Geometries3D.GetPoints(Document),"point",scope); Add(TopSolidHost.Geometries2D.GetPoints(Document),"point2d",scope);
            Add(TopSolidHost.Geometries3D.GetFrames(Document),"frame",scope); Add(TopSolidHost.Geometries2D.GetFrames(Document),"frame2d",scope);
            Add(TopSolidHost.Geometries3D.GetAxes(Document),"axis",scope); Add(TopSolidHost.Geometries2D.GetAxes(Document),"axis2d",scope);
            Add(TopSolidHost.Geometries3D.GetPlanes(Document),"plane",scope);
            foreach (var id in scope ?? new HashSet<ElementId>(TopSolidHost.Elements.GetElements(Document)))
            {
                var type = TopSolidHost.Elements.GetTypeFullName(id);
                // GetShapes(document) omits included workpiece shapes. Keep the verified
                // native occurrence in this document, never its source-document geometry.
                if ((scope != null || allCamGeometry) && type.EndsWith(".Shapes.ShapeEntity",StringComparison.Ordinal)) Add(new[] {id},"shape",scope);
                if (type.IndexOf("LimitedProfile",StringComparison.OrdinalIgnoreCase)>=0) Add(new[] {id},"limitedProfile",scope);
                else if (type.EndsWith("ProfileEntity",StringComparison.Ordinal)) Add(new[] {id},"profile",scope);
            }
            foreach (var pair in kinds.OrderBy(p => p.Key.Id))
            {
                Targets.Add(new JObject { ["element"] = AutomationValues.Json(pair.Key) });
                if (Targets.Count>20000) throw new ArgumentException("Geometry exceeds the inspection limit. Select a smaller workpiece.");
                if (pair.Value != "shape") continue;
                var items = TopSolidHost.Shapes.GetFaces(pair.Key); faces.Add(pair.Key,items);
                foreach (var face in items) Targets.Add(new JObject { ["face"] = AutomationValues.Json(face) });
                if (Targets.Count>20000) throw new ArgumentException("Geometry exceeds the inspection limit. Select a smaller workpiece.");
            }
        }
        private HashSet<ElementId> CamWorkpieceScope(ElementId part)
        {
            if (!TopSolidCamHost.Parts.IsPart(part)) throw new ArgumentException("This workpiece does not expose native preparation faces through IParts. Faceted workpieces require a verified native face mapping and are unavailable for automatic color preparation.");
            var scope = Descendants(part);
            // Some CAM part entities have no constituents. PartFinish is a native
            // composite reference, so GetValue returns null on 7.20. Accept only its
            // exact same-document identity serialization, verified against native elements.
            var finish = TopSolidCamHost.Parts.GetParameters(part).Where(p => TopSolidCamHost.Parameters.GetName(p)=="PartFinish").ToArray();
            if (finish.Length!=1) throw new InvalidOperationException("The CAM workpiece has no unambiguous native finished-geometry reference. Open its design part for color preparation.");
            var reference = TopSolidCamHost.Parameters.ToInvariantStringValue(finish[0]);
            var elements = TopSolidHost.Elements.GetElements(Document);
            var ids = elements.Where(id => MatchesFinishReference(reference,Document.PdmDocumentId,id.Id)).ToArray();
            if(ids.Length!=1) throw new InvalidOperationException("This CAM finished-geometry reference cannot be resolved safely. Open its design part for color preparation.");
            scope.UnionWith(Descendants(ids[0]));
            return scope;
        }
        internal static bool MatchesFinishReference(string reference,string document,int id) =>
            string.Equals(reference,document+"::"+id.ToString(global::System.Globalization.CultureInfo.InvariantCulture),StringComparison.Ordinal);
        private static HashSet<ElementId> Descendants(ElementId root)
        {
            var seen = new HashSet<ElementId>(); var pending = new Queue<ElementId>(); pending.Enqueue(root);
            while(pending.Count>0) {
                var id=pending.Dequeue(); if(!seen.Add(id)) continue;
                if(seen.Count>20000) throw new ArgumentException("Workpiece scope is too large.");
                foreach(var child in TopSolidHost.Elements.GetConstituents(id)) if(!child.IsEmpty) pending.Enqueue(child);
            }
            return seen;
        }
        private void Add(IEnumerable<ElementId> ids,string kind,HashSet<ElementId> scope)
        { foreach(var id in ids) if(!id.IsEmpty && (scope==null || scope.Contains(id)) && !kinds.ContainsKey(id)) kinds.Add(id,kind); }

        internal static JObject Page(AutomationGateway a,JObject input)
        {
            var geometry=new CamColorGeometry(a,input); var offset=(int?)input["offset"]??0; var limit=(int?)input["limit"]??20;
            if (input["targets"] is JArray selected)
            {
                if (input["offset"] != null || input["limit"] != null || selected.Count < 1 || selected.Count > 20 ||
                    selected.Any(t => t is not JObject obj || obj.Count != 1 || obj["element"] == null && obj["face"] == null) ||
                    selected.Select(t => t.ToString(Formatting.None)).Distinct(StringComparer.Ordinal).Count() != selected.Count)
                    throw new ArgumentException("Inspect 1..20 distinct exact targets without pagination arguments.");
                return new JObject { ["documentId"] = geometry.Document.PdmDocumentId, ["items"] = new JArray(selected.OfType<JObject>().Select(t => Bounded(geometry.Read(t)))),
                    ["total"] = selected.Count, ["offset"] = 0, ["hasMore"] = false };
            }
            var items=new JArray(geometry.Targets.Skip(offset).Take(limit).Select(t=>Bounded(geometry.Read(t))));
            var next=offset+items.Count;
            return new JObject { ["documentId"]=geometry.Document.PdmDocumentId, ["name"]=TopSolidHost.Documents.GetName(geometry.Document),
                ["workpieces"]=geometry.Workpieces, ["needsWorkpiece"]=geometry.NeedsWorkpiece, ["items"]=items, ["total"]=geometry.Targets.Count,
                ["offset"]=offset, ["hasMore"]=next<geometry.Targets.Count, ["nextOffset"]=next<geometry.Targets.Count?(JToken)new JValue(next):JValue.CreateNull(),
                ["units"]="metres; square metres; cubic metres" };
        }
        internal JObject Read(JObject target)
        {
            var face=target["face"]!=null;
            var item=face ? gateway.Item(new JObject { ["item"]=target["face"].DeepClone() }) : default(ElementItemId);
            var id=face ? item.ElementId : gateway.Element(target);
            if(!kinds.TryGetValue(id,out var kind) || face && (!faces.TryGetValue(id,out var valid) || !valid.Contains(item)))
                throw new ArgumentException("Geometry is outside the selected workpiece or its topology changed.");
            if(!TopSolidHost.Elements.Exists(id) || TopSolidHost.Elements.IsInvalid(id)) throw new ArgumentException("Invalid geometry target.");
            var data=face ? Face(item) : Element(id,kind);
            var color=face ? ElementAppearance.Json(TopSolidHost.Shapes.GetFaceColor(item)) : ElementAppearance.Read(TopSolidHost.Elements,id)["color"];
            // IsModifiable describes direct entity editing: feature-generated shapes
            // may return false while supporting native coloring operations.
            var supported=TopSolidHost.Elements.IsColorModifiable(id) && (bool?)data["verified"]!=false;
            var name=TopSolidHost.Elements.GetFriendlyName(id);
            var row=new JObject { ["target"]=target.DeepClone(), ["kind"]=face?"face":kind, ["name"]=face?name+" · "+(faces[id].IndexOf(item)+1):name,
                ["type"]=TopSolidHost.Elements.GetTypeFullName(id), ["color"]=color==null||color.Type==JTokenType.Null?new JObject { ["empty"]=true }:color.DeepClone(),
                ["colorSupported"]=supported, ["supportReason"]=supported?"":(string)data["unsupportedReason"]??(face ? "The face owner is not natively modifiable in this CAM document. Prepare an editable native workpiece before applying colors." : "TopSolid does not allow this entity's color to be modified."), ["geometry"]=data };
            row["key"]=Key(target);
            var geometryOnly=(JObject)row.DeepClone(); geometryOnly.Remove("color");
            ((JObject)geometryOnly["geometry"]).Remove("facesFingerprint");
            ((JObject)geometryOnly["geometry"]).Remove("faceOverrides");
            row["geometryFingerprint"]=Fingerprint(geometryOnly);
            row["fingerprint"]=Fingerprint(row); return row;
        }
        // Fingerprint all observed facts before bounding transport arrays. Truncation
        // is explicit; the model must leave insufficiently described geometry unassigned.
        private static JObject Bounded(JObject row)
        {
            var data=(JObject)row["geometry"];
            foreach(var obj in data.DescendantsAndSelf().OfType<JObject>().ToArray())
                foreach(var property in obj.Properties().ToArray())
                    if(property.Value is JArray array && array.Count>64) {
                        obj[property.Name+"Count"]=array.Count;
                        obj[property.Name+"Truncated"]=true;
                        property.Value=new JArray(array.Take(64).Select(v=>v.DeepClone()));
                        data["detailsTruncated"]=true;
                    }
            return row;
        }
        internal static string Key(JObject target) => target.ToString(Formatting.None);
        internal static string Fingerprint(JObject row)
        {
            var copy=(JObject)row.DeepClone(); copy.Remove("key"); copy.Remove("fingerprint");
            foreach(var obj in copy.DescendantsAndSelf().OfType<JObject>()) obj.Remove("documentId");
            using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Canonical(copy).ToString(Formatting.None)))).Replace("-","");
        }
        private static JToken Canonical(JToken value) => value is JObject obj ? new JObject(obj.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new JProperty(p.Name,Canonical(p.Value))))
            : value is JArray array ? new JArray(array.Select(Canonical)) : value.DeepClone();
        private static JObject Face(ElementItemId face)
        {
            var shapes=TopSolidHost.Shapes; var type=shapes.GetFaceSurfaceType(face);
            shapes.GetFaceEnclosingCoordinates(face,out var xmin,out var xmax,out var ymin,out var ymax,out var zmin,out var zmax);
            var data=new JObject { ["surfaceType"]=type.ToString(), ["area"]=shapes.GetFaceArea(face), ["bounds"]=new JArray(xmin,ymin,zmin,xmax,ymax,zmax),
                ["adjacentFaces"]=AutomationValues.Json(shapes.GetFaceConnectedFaces(face)), ["vertices"]=new JArray(shapes.GetFaceVertices(face).Select(v=>AutomationValues.Json(shapes.GetVertexPoint(v)))) };
            if(type==SurfaceType.Plane) { shapes.GetFacePlaneSurface(face,out var plane); data["plane"]=AutomationValues.Json(plane); }
            if(type==SurfaceType.Cylinder) { shapes.GetFaceCylinderSurface(face,out var frame,out var radius); data["frame"]=AutomationValues.Json(frame); data["radius"]=radius; data["length"]=shapes.GetFaceCylinderLength(face); }
            if(type==SurfaceType.Sphere) { shapes.GetFaceSphereSurface(face,out var frame,out var radius); data["frame"]=AutomationValues.Json(frame); data["radius"]=radius; }
            data["reversed"]=shapes.IsFaceReversed(face);
            shapes.GetFaceRange(face,out var u0,out var u1,out var v0,out var v1);
            data["parameterRange"]=new JArray(u0,u1,v0,v1);
            data["surfaceSamples"]=new JArray(Enumerable.Range(0,3).SelectMany(u=>Enumerable.Range(0,3).Select(v=>
                AutomationValues.Json(shapes.GetFacePoint(face,u0+(u1-u0)*u/2,v0+(v1-v0)*v/2)))));
            data["edges"]=new JArray(shapes.GetFaceEdges(face).Select(e=>new JObject { ["item"]=AutomationValues.Json(e), ["length"]=shapes.GetEdgeLength(e), ["curveType"]=shapes.GetEdgeCurveType(e).ToString() }));
            return data;
        }
        private JObject Element(ElementId id,string kind)
        {
            var data=new JObject();
            if(kind=="shape") {
                var type=TopSolidHost.Shapes.GetShapeType(id); data["shapeType"]=type.ToString();
                if(type==ShapeType.Solid) data["volume"]=TopSolidHost.Shapes.GetShapeVolume(id);
                var items=faces.TryGetValue(id,out var cached)?cached:TopSolidHost.Shapes.GetFaces(id);
                data["faceCount"]=items.Count;
                // Include every face's geometry/color in the hash without sending an unbounded nested payload.
                var facts=new JArray(items.Select(f=>new JObject { ["face"]=AutomationValues.Json(f), ["geometry"]=Face(f), ["color"]=ElementAppearance.Json(TopSolidHost.Shapes.GetFaceColor(f)) }));
                data["facesFingerprint"]=Fingerprint(new JObject { ["faces"]=facts });
                data["topologyFingerprint"]=Fingerprint(new JObject { ["faces"]=new JArray(facts.OfType<JObject>().Select(f => new JObject { ["face"]=f["face"], ["geometry"]=f["geometry"] })) });
                if(facts.Count>0) data["bounds"]=new JArray(Enumerable.Range(0,6).Select(i=>i<3?facts.Min(f=>(double)f["geometry"]["bounds"][i]):facts.Max(f=>(double)f["geometry"]["bounds"][i])));
                data["faceOverrides"]=items.Count(f=>!TopSolidHost.Shapes.GetFaceColor(f).IsEmpty);
            }
            else if(kind=="sketch2d" || kind=="sketch3d") {
                var two=kind=="sketch2d"; var profiles=two?TopSolidHost.Sketches2D.GetProfiles(id):TopSolidHost.Sketches3D.GetProfiles(id);
                data["colorScope"]="wholeSketch";
                data["profiles"]=new JArray(profiles.Select(p=>new JObject { ["profile"]=AutomationValues.Json(p), ["closed"]=two?TopSolidHost.Sketches2D.IsProfileClosed(p):TopSolidHost.Sketches3D.IsProfileClosed(p),
                    ["segments"]=AutomationValues.Json(two?TopSolidHost.Sketches2D.GetProfileSegments(p):TopSolidHost.Sketches3D.GetProfileSegments(p)) }));
                data["frame"]=two?AutomationValues.Json(TopSolidHost.Sketches2D.GetFrame(id)):AutomationValues.Json(TopSolidHost.Sketches3D.GetFrame(id));
                var vertices=two?TopSolidHost.Sketches2D.GetVertices(id):TopSolidHost.Sketches3D.GetVertices(id);
                data["vertices"]=new JArray(vertices.Select(v=>two?AutomationValues.Json(TopSolidHost.Sketches2D.GetVertexPoint(v)):AutomationValues.Json(TopSolidHost.Sketches3D.GetVertexPoint(v))));
                var segments=two?TopSolidHost.Sketches2D.GetSegments(id):TopSolidHost.Sketches3D.GetSegments(id);
                data["segments"]=new JArray(segments.Select(s=>Segment(s,two)));
                if(two && TopSolidHost.Elements.GetTypeFullName(id).Contains(".D3.")) {
                    var plane=TopSolidHost.Sketches2D.GetPlane(id); data["plane"]=AutomationValues.Json(plane);
                    var points=((JArray)data["segments"]).SelectMany(s=>(JArray)s["samples"]??new JArray()).OfType<JObject>().Select(p=>new Point3D(
                        plane.Origin.X+(double)p["X"]*plane.XDirection.X+(double)p["Y"]*plane.YDirection.X,
                        plane.Origin.Y+(double)p["X"]*plane.XDirection.Y+(double)p["Y"]*plane.YDirection.Y,
                        plane.Origin.Z+(double)p["X"]*plane.XDirection.Z+(double)p["Y"]*plane.YDirection.Z)).ToArray();
                    if(points.Length>0) { data["bounds"]=new JArray(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z),points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z));data["boundsKind"]="native curve samples"; }
                }
            }
            else if(kind=="point") data["point"]=AutomationValues.Json(TopSolidHost.Geometries3D.GetPointGeometry(id));
            else if(kind=="point2d") data["point"]=AutomationValues.Json(TopSolidHost.Geometries2D.GetPointGeometry(id));
            else if(kind=="frame") data["frame"]=AutomationValues.Json(TopSolidHost.Geometries3D.GetFrameGeometry(id));
            else if(kind=="frame2d") data["frame"]=AutomationValues.Json(TopSolidHost.Geometries2D.GetFrameGeometry(id));
            else if(kind=="axis") data["axis"]=AutomationValues.Json(TopSolidHost.Geometries3D.GetAxisGeometry(id));
            else if(kind=="axis2d") data["axis"]=AutomationValues.Json(TopSolidHost.Geometries2D.GetAxisGeometry(id));
            else if(kind=="plane") data["plane"]=AutomationValues.Json(TopSolidHost.Geometries3D.GetPlaneGeometry(id));
            else {
                data["constituents"]=AutomationValues.Json(TopSolidHost.Elements.GetConstituents(id));
                // Native profiles remain individually identified. The current public SDK
                // does not expose their curve definition, so a safe stale-geometry check
                // cannot be made unless they are an owning sketch returned above.
                data["verified"]=false;
                data["unsupportedReason"]="The native API does not expose a verifiable curve definition for this standalone profile. Color it in TopSolid's native editor.";
            }
            return data;
        }
        private static JObject Segment(ElementItemId id,bool two)
        {
            double t0,t1;
            if(two) TopSolidHost.Sketches2D.GetSegmentRange(id,out t0,out t1); else TopSolidHost.Sketches3D.GetSegmentRange(id,out t0,out t1);
            var type=two?TopSolidHost.Sketches2D.GetSegmentCurveType(id):TopSolidHost.Sketches3D.GetSegmentCurveType(id);
            // IsSegmentConstruction requires StartModification on this SDK, despite
            // its getter-like name. Inspection must never enter sketch modification.
            var data=new JObject { ["segment"]=AutomationValues.Json(id),["curveType"]=type.ToString() };
            if(type==CurveType.Line) {
                if(two) {TopSolidHost.Sketches2D.GetSegmentLineCurve(id,out var axis);data["axis"]=AutomationValues.Json(axis);}
                else {TopSolidHost.Sketches3D.GetSegmentLineCurve(id,out var axis);data["axis"]=AutomationValues.Json(axis);}
            }
            if(type==CurveType.Circle) {
                if(two) {TopSolidHost.Sketches2D.GetSegmentCircleCurve(id,out var frame,out var radius);data["frame"]=AutomationValues.Json(frame);data["radius"]=radius;}
                else {TopSolidHost.Sketches3D.GetSegmentCircleCurve(id,out var plane,out var radius);data["plane"]=AutomationValues.Json(plane);data["radius"]=radius;}
            }
            if(double.IsInfinity(t0)||double.IsInfinity(t1)) {data["rangeKind"]="unbounded";return data;}
            if(double.IsNaN(t0)||double.IsNaN(t1)) throw new InvalidOperationException("Native curve parameters are invalid.");
            data["range"]=new JArray(t0,t1);
            data["samples"]=new JArray(Enumerable.Range(0,17).Select(i=>two?AutomationValues.Json(TopSolidHost.Sketches2D.GetSegmentPoint(id,t0+(t1-t0)*i/16)):
                AutomationValues.Json(TopSolidHost.Sketches3D.GetSegmentPoint(id,t0+(t1-t0)*i/16))));
            return data;
        }
    }
}
