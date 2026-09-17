// Generated from the reviewed allowlist in scripts/ApiReference/generate_read_tools.py.
// Each call is compiled against the matched 7.20 SDK. No generic API invocation.
using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolid.Cad.Drafting.Automating;
using TopSolid.Cad.Electrode.Automating;
using TopSolid.Cam.NC.Kernel.Automating;
using TopSolid.Cae.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class Design3DReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_points3d", "List points3d.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Geometries3D.GetPoints(a.Document(p)), p)), "Design3D", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries3D.GetPoints.html" }));
            register(new ToolDefinition("topsolid_get_point3d", "Read point coordinates in metres.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Geometries3D.GetPointGeometry(a.Element(p)))), "Design3D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries3D.GetPointGeometry.html" }));
            register(new ToolDefinition("topsolid_list_shapes", "List shapes.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Shapes.GetShapes(a.Document(p)), p)), "Design3D", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetShapes.html" }));
            register(new ToolDefinition("topsolid_get_shape_volume", "Read shape volume. Length, area and volume use SI metres, square metres and cubic metres.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Shapes.GetShapeVolume(a.Element(p)))), "Design3D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetShapeVolume.html" }));
            register(new ToolDefinition("topsolid_get_shape_type", "Read shape type. Length, area and volume use SI metres, square metres and cubic metres.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Shapes.GetShapeType(a.Element(p)))), "Design3D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetShapeType.html" }));
            register(new ToolDefinition("topsolid_get_shape_vertex_point", "Read shape vertex point. Length, area and volume use SI metres, square metres and cubic metres.", new JObject { ["item"] = Schema.Item() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Shapes.GetVertexPoint(a.Item(p)))), "Design3D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetVertexPoint.html" }));
            register(new ToolDefinition("topsolid_get_face_area", "Read face area. Length, area and volume use SI metres, square metres and cubic metres.", new JObject { ["item"] = Schema.Item() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Shapes.GetFaceArea(a.Item(p)))), "Design3D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetFaceArea.html" }));
            register(new ToolDefinition("topsolid_get_edge_curve_type", "Read edge curve type. Length, area and volume use SI metres, square metres and cubic metres.", new JObject { ["item"] = Schema.Item() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Shapes.GetEdgeCurveType(a.Item(p)))), "Design3D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetEdgeCurveType.html" }));
            register(new ToolDefinition("topsolid_list_shape_faces", "List shape faces.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Shapes.GetFaces(a.Element(p)), p)), "Design3D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetFaces.html" }));
            register(new ToolDefinition("topsolid_list_shape_edges", "List shape edges.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Shapes.GetEdges(a.Element(p)), p)), "Design3D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetEdges.html" }));
            register(new ToolDefinition("topsolid_list_shape_vertices", "List shape vertices.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Shapes.GetVertices(a.Element(p)), p)), "Design3D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetVertices.html" }));
        }
    }
}
