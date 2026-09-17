using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class PointBatchTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            foreach (var dimension in new[] { 2, 3 }) foreach (var create in new[] { false, true })
            {
                var d = dimension; var isCreate = create;
                var point = new JObject { ["x"] = Schema.Number("X in input units."), ["y"] = Schema.Number("Y in input units.") };
                if (d == 3) point["z"] = Schema.Number("Z in input units.");
                var entry = new JObject { ["point"] = Schema.Object(point, d == 3 ? new[] { "x", "y", "z" } : new[] { "x", "y" }) };
                if (create) entry["name"] = Schema.Text("Optional new point name.", 128); else entry["element"] = Schema.Element();
                var props = DocumentActionTools.Target(); props["units"] = Schema.Choice("Length units; default mm.", "mm", "cm", "m");
                props["points"] = Schema.Array(Schema.Object(entry, create ? new[] { "point" } : new[] { "point", "element" }), 1, BatchInput.MaximumChanges);
                register(new ToolDefinition("topsolid_" + (create ? "create" : "update") + "_points" + d + "d",
                    (create ? "Create " : "Set geometry of existing ") + d + "D point entities in one confirmed modification. Up to 32 points. These are document point entities, not sketch vertices. Does not save.", props,
                    p => a.Modify(p, (isCreate ? "create" : "update") + " points", "kernel", (doc, current) =>
                    {
                        var scale = ModelingGeometry.Scale(current); var rows = new JArray();
                        foreach (JObject value in current["points"])
                        {
                            var point2 = new Point2D((double)value["point"]["x"] * scale, (double)value["point"]["y"] * scale);
                            var point3 = new Point3D(point2.X, point2.Y, ((double?)value["point"]["z"] ?? 0) * scale);
                            ElementId id;
                            if (isCreate)
                            {
                                id = d == 2 ? TopSolidHost.Geometries2D.CreatePoint(doc, point2) : TopSolidHost.Geometries3D.CreatePoint(doc, point3);
                                AutomationGateway.RequireValid(id, "point"); if (value["name"] != null) TopSolidHost.Elements.SetName(id, (string)value["name"]);
                            }
                            else { id = a.Element(value); if (d == 2) TopSolidHost.Geometries2D.SetPointGeometry(id, point2); else TopSolidHost.Geometries3D.SetPointGeometry(id, point3); }
                            var row = EntityBatchReadTools.Identity(id); row["pointMetres"] = Point(id, d); rows.Add(row);
                        }
                        return new JObject { ["items"] = rows, ["changed"] = rows.Count };
                    }), "Design" + d + "D", new[] { "documentId", "points" }, false,
                    ApiRefs.Kernel("IGeometries" + d + "D.CreatePoint", "IGeometries" + d + "D.SetPointGeometry", "IGeometries" + d + "D.GetPointGeometry", "IElements.SetName"),
                    p => { MutationReferences.Validate(p); if (!isCreate) BatchInput.Elements(p, "points", true); },
                    p =>
                    {
                        var preview = a.PreviewDocument(p);
                        if (!isCreate) preview["currentPoints"] = new JArray(((JArray)p["points"]).Cast<JObject>().Select(v => new JObject { ["element"] = v["element"].DeepClone(), ["pointMetres"] = Point(a.Element(v), d) }));
                        return preview;
                    }, defaultLengthUnits: "mm"));
            }
        }
        private static JToken Point(ElementId id, int d) => d == 2 ? AutomationValues.Json(TopSolidHost.Geometries2D.GetPointGeometry(id)) : AutomationValues.Json(TopSolidHost.Geometries3D.GetPointGeometry(id));
    }
}
