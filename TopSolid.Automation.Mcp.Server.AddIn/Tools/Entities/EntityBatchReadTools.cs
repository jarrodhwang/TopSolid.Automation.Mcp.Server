using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class EntityBatchReadTools
    {
        internal static readonly string[] InfoApi = ObjectIdentity.ElementApi.Concat(ApiRefs.Kernel("IElements.IsInvalid", "IElements.IsVisible", "IElements.IsDeletable")).ToArray();
        internal static JObject Identity(ElementId id) => new JObject { ["element"] = AutomationValues.Json(id) };
        internal static JObject Info(ElementId id) {
            var row = ObjectIdentity.Element(id); row["displayName"] = row["friendlyName"].DeepClone();
            row["invalid"] = TopSolidHost.Elements.IsInvalid(id); row["visible"] = TopSolidHost.Elements.IsVisible(id); row["deletable"] = TopSolidHost.Elements.IsDeletable(id); return row;
        }
        internal static JObject Parameter(ElementId id)
        {
            var value = EntityDetailsTools.Value(id); value["name"] = TopSolidHost.Elements.GetName(id); value["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(id); return value;
        }
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_inspect_elements", "Read names, types, visibility, validity and deletability for up to 100 exact handles in one request. Per-item failures are explicit. Follow nextOffset when hasMore=true.",
                BatchRead.Paging(new JObject { ["elements"] = Schema.Array(Schema.Element(), 1, 100) }),
                p => a.Read("kernel", () => BatchRead.Page((JArray)p["elements"], p, id => new JObject { ["element"] = id.DeepClone() },
                    id => Info(a.Element(new JObject { ["element"] = id.DeepClone() })))), "Entities", new[] { "elements" }, api: InfoApi));
            register(new ToolDefinition("topsolid_list_named_elements", "List document elements with friendly names, types and state together. Prefer this over listing IDs then inspecting each element. Use kind to narrow the query; follow nextOffset until hasMore=false.",
                BatchRead.Paging(new JObject { ["documentId"] = Schema.Text("Exact document revision; default active."),
                    ["kind"] = Schema.Choice("Element collection; default all.", "all", "sketches2d", "sketches3d", "shapes", "operations", "points2d", "points3d") }),
                p => a.Read("kernel", () =>
                {
                    var doc = a.Document(p);
                    var kind = (string)p["kind"] ?? "all";
                    var ids = kind == "sketches2d" ? TopSolidHost.Sketches2D.GetSketches(doc) : kind == "sketches3d" ? TopSolidHost.Sketches3D.GetSketches(doc)
                        : kind == "shapes" ? TopSolidHost.Shapes.GetShapes(doc) : kind == "operations" ? TopSolidHost.Operations.GetOperations(doc)
                        : kind == "points2d" ? TopSolidHost.Geometries2D.GetPoints(doc) : kind == "points3d" ? TopSolidHost.Geometries3D.GetPoints(doc) : TopSolidHost.Elements.GetElements(doc);
                    return BatchRead.Page(ids, p, Identity, Info);
                }), "Entities", api: InfoApi.Concat(ApiRefs.Kernel("IElements.GetElements", "ISketches2D.GetSketches", "ISketches3D.GetSketches", "IShapes.GetShapes", "IOperations.GetOperations", "IGeometries2D.GetPoints", "IGeometries3D.GetPoints")).ToArray()));
            register(new ToolDefinition("topsolid_list_parameter_values", "List parameter names, values, types and SI unit metadata together. Avoid one get_parameter_value call per parameter. Follow nextOffset until hasMore=false; unsupported value types are marked.",
                BatchRead.Paging(new JObject { ["documentId"] = Schema.Text("Document revision; default active.") }),
                p => a.Read("kernel", () => BatchRead.Page(TopSolidHost.Parameters.GetParameters(a.Document(p)), p, Identity, Parameter)), "Entities",
                api: ApiRefs.Kernel("IParameters.GetParameters", "IParameters.GetParameterType", "IParameters.GetRealValue", "IParameters.GetRealUnit", "IParameters.GetIntegerValue", "IParameters.GetBooleanValue", "IParameters.GetTextValue", "IParameters.GetDateTimeValue", "IParameters.GetEnumerationText", "IParameters.GetUserEnumerationText", "IElements.GetName", "IElements.GetFriendlyName")));
        }
    }
}
