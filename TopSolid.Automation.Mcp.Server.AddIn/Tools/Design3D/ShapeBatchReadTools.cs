using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ShapeBatchReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_shape_summaries", "List shape names, types, face/edge/vertex counts and solid volumes in one page. SI volume is cubic metres. Follow nextOffset; does not call the model for each shape.",
                BatchRead.Paging(new JObject { ["documentId"] = Schema.Text("Document revision; default active.") }),
                p => a.Read("kernel", () => BatchRead.Page(TopSolidHost.Shapes.GetShapes(a.Document(p)), p, EntityBatchReadTools.Identity, id =>
                {
                    var type = TopSolidHost.Shapes.GetShapeType(id);
                    var row = new JObject { ["name"] = TopSolidHost.Elements.GetFriendlyName(id), ["shapeType"] = type.ToString(),
                        ["faces"] = TopSolidHost.Shapes.GetFaceCount(id), ["edges"] = TopSolidHost.Shapes.GetEdgeCount(id), ["vertices"] = TopSolidHost.Shapes.GetVertexCount(id) };
                    if (type == ShapeType.Solid) row["volumeCubicMetres"] = TopSolidHost.Shapes.GetShapeVolume(id);
                    return row;
                })), "Design3D", api: ApiRefs.Kernel("IShapes.GetShapes", "IShapes.GetShapeType", "IShapes.GetShapeVolume", "IShapes.GetFaceCount", "IShapes.GetEdgeCount", "IShapes.GetVertexCount", "IElements.GetFriendlyName")));
        }
    }
}
