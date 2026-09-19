using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        public JObject PreviewModeling(JObject arguments)
        {
            var preview = PreviewDocument(arguments);
            if (TopSolidHost.Application.Version < TopSolidVersionSupport.ModelingMinimumVersion) throw new InvalidOperationException("Modeling requires TopSolid 7.20.326 or newer.");
            if (arguments["documentId"] == null) throw new ArgumentException("Modeling requires an explicit documentId.");
            return preview;
        }

        public JObject CreateModel(string operation, JObject arguments)
        {
            EnsureConnected();
            if (TopSolidHost.Application.Version < TopSolidVersionSupport.ModelingMinimumVersion) throw new InvalidOperationException("Modeling requires TopSolid 7.20.326 or newer for native sketch cleanup.");
            var document = Document(arguments);
            var originalDocumentId = document.PdmDocumentId;
            var scale = ModelingGeometry.Scale(arguments);
            double Value(string name) => ((double?)arguments[name] ?? 0) * scale;
            ModelingGeometry.Validate(operation, arguments);
            return ModificationScope.Run("AI: " + operation,
                title => TopSolidHost.Application.StartModification(title, false), TopSolidHost.Application.EndModification,
                () =>
                {
                    TopSolidHost.Documents.EnsureIsDirty(ref document);
                    ElementId sketch;
                    ElementItemId profile = ElementItemId.Empty;
                    if (operation == "polyline3d")
                    {
                        sketch = TopSolidHost.Sketches3D.CreateSketch(document, SmartPlane3D.OXY,
                            new SmartPoint3D(new Point3D(0, 0, 0)), true, new SmartDirection3D(new Direction3D(1, 0, 0), new Point3D(0, 0, 0)));
                        TopSolidHost.Sketches3D.StartModification(sketch);
                        try
                        {
                            var points = ((JArray)arguments["points"]).Select(v => new Point3D((double)v["x"] * scale, (double)v["y"] * scale, (double)v["z"] * scale)).ToList();
                            var vertices = points.Select(TopSolidHost.Sketches3D.CreateVertex).ToList();
                            var segments = new List<ElementItemId>();
                            for (var i = 1; i < vertices.Count; i++) segments.Add(TopSolidHost.Sketches3D.CreateLineSegment(vertices[i - 1], vertices[i]));
                            if ((bool?)arguments["closed"] ?? false)
                            {
                                segments.Add(TopSolidHost.Sketches3D.CreateLineSegment(vertices[vertices.Count - 1], vertices[0]));
                                TopSolidHost.Sketches3D.CreateProfile(segments);
                            }
                            else TopSolidHost.Sketches3D.CleanSketch();
                        }
                        finally { TopSolidHost.Sketches3D.EndModification(); }
                        RequireValid(TopSolidHost.Sketches3D.CreateBuildingOperation(sketch), "3D sketch building operation");
                    }
                    else
                    {
                        var placement = operation == "extruded_rectangle" ? "xy" : (string)arguments["placement"];
                        if (placement == "2d")
                            sketch = TopSolidHost.Sketches2D.CreateSketchIn2D(document, new SmartPoint2D(new Point2D(Value("x"), Value("y"))), true, new SmartDirection2D(new Direction2D(1, 0), new Point2D(Value("x"), Value("y"))));
                        else
                        {
                            var origin = new Point3D(Value("x"), Value("y"), Value("z"));
                            var direction = placement == "yz" ? new Direction3D(0, 1, 0) : new Direction3D(1, 0, 0);
                            var extent = Math.Max(0.01, Math.Max(Value("width"), Math.Max(Value("height"), Value("radius"))) * 2);
                            var plane = new SmartPlane3D(ModelingGeometry.Plane(placement, origin), -extent, extent, -extent, extent);
                            sketch = TopSolidHost.Sketches2D.CreateSketchIn3D(document, plane,
                                new SmartPoint3D(origin), true, new SmartDirection3D(direction, origin));
                        }
                        TopSolidHost.Sketches2D.StartModification(sketch);
                        try
                        {
                            var segments = new List<ElementItemId>();
                            if (operation == "circle2d")
                                segments.Add(TopSolidHost.Sketches2D.CreateCircleSegment(TopSolidHost.Sketches2D.CreateVertex(new Point2D(0, 0)), Value("radius")));
                            else
                            {
                                var points = new[] { new Point2D(0, 0), new Point2D(Value("width"), 0), new Point2D(Value("width"), Value("height")), new Point2D(0, Value("height")) };
                                var vertices = points.Select(TopSolidHost.Sketches2D.CreateVertex).ToArray();
                                for (var i = 0; i < 4; i++) segments.Add(TopSolidHost.Sketches2D.CreateLineSegment(vertices[i], vertices[(i + 1) % 4]));
                            }
                            profile = TopSolidHost.Sketches2D.CreateProfile(segments);
                        }
                        finally { TopSolidHost.Sketches2D.EndModification(); }
                        RequireValid(TopSolidHost.Sketches2D.CreateBuildingOperation(sketch), "2D sketch building operation");
                    }
                    RequireValid(sketch, "sketch");
                    var segmentCount = operation == "polyline3d" ? TopSolidHost.Sketches3D.GetSegmentCount(sketch) : TopSolidHost.Sketches2D.GetSegmentCount(sketch);
                    if (segmentCount == 0 || (operation != "polyline3d" && profile.IsEmpty)) throw new InvalidOperationException("Native sketch topology is empty.");
                    CreationNames.SetAndVerify(TopSolidHost.Elements, sketch, (string)arguments["name"]);
                    var result = new JObject { ["originalDocumentId"] = originalDocumentId, ["documentId"] = document.PdmDocumentId, ["sketch"] = AutomationValues.Json(sketch), ["saved"] = false, ["units"] = "metres", ["message"] = "Created native geometry in the specified document. Changes are not saved automatically." };
                    result["profile"] = AutomationValues.Json(profile);
                    result["name"] = TopSolidHost.Elements.GetName(sketch);
                    result["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(sketch);
                    result["sectionCreated"] = false;
                    result["nativeSegmentCount"] = segmentCount;
                    result["geometryReadBack"] = true;
                    if (operation == "extruded_rectangle")
                    {
                        // SmartSection3D(sketch) references the existing sketch. It does
                        // not create a persistent ISketches2D section in that sketch.
                        var shape = TopSolidHost.Shapes.CreateExtrudedShape(document, new SmartSection3D(sketch),
                            new SmartDirection3D(new Direction3D(0, 0, 1), new Point3D(Value("x"), Value("y"), Value("z"))), new SmartReal(UnitType.Length, Value("depth")), null, false, false);
                        result["shape"] = AutomationValues.Json(shape);
                        RequireValid(shape, "extruded shape");
                        var volume = TopSolidHost.Shapes.GetShapeVolume(shape);
                        if (!(volume > 0) || double.IsInfinity(volume)) throw new InvalidOperationException("Native extrusion has no positive solid volume.");
                        result["volumeCubicMetres"] = volume;
                    }
                    if (operation != "polyline3d") RequireUnchangedSections(sketch, 0);
                    return result;
                });
        }
    }
}
