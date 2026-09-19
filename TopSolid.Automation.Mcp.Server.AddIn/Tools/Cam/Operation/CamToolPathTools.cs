using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CamToolPathTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_read_cam_toolpath", "Read a bounded page of an existing CAM operation's toolpath table. Does not calculate a toolpath. Toolpath columns vary by machining strategy; results are not a safety verification.",
                new JObject { ["element"] = Schema.Element(), ["offset"] = Schema.Integer("Rows to skip; default 0. Scans are bounded to the first 2000 rows.", 0, 1900), ["limit"] = Schema.Integer("Rows to return; default 25.", 1, 100) },
                p => a.Read("cam", () =>
                {
                    var id = a.Element(p);
                    var columns = TopSolidCamHost.ToolPath.StartToolPath(id);
                    if (columns == null) return new JObject { ["available"] = false, ["message"] = "No existing toolpath is available for this operation." };
                    try
                    {
                        var offset = (int?)p["offset"] ?? 0; var limit = (int?)p["limit"] ?? 25;
                        var rows = new JArray(); var exhausted = false;
                        for (var i = 0; i < offset + limit; i++)
                        {
                            var row = TopSolidCamHost.ToolPath.NextToolPathItem(id);
                            if (row == null) { exhausted = true; break; }
                            if (i >= offset) rows.Add(new JObject(row.Select(pair => new JProperty(pair.Key, Value(pair.Value)))));
                        }
                        var more = !exhausted && TopSolidCamHost.ToolPath.NextToolPathItem(id) != null;
                        return new JObject { ["available"] = true, ["columns"] = new JArray(columns), ["rows"] = rows, ["offset"] = offset, ["hasMore"] = more,
                            ["nextOffset"] = more && offset + rows.Count <= 1900 ? new JValue(offset + rows.Count) : JValue.CreateNull(),
                            ["scanLimitReached"] = more && offset + rows.Count > 1900,
                            ["units"] = "Per IToolPath: distances metres; angles radians; linear speeds metres/second; revolution speeds revolutions/minute. Other columns retain their documented API meaning." };
                    }
                    finally { TopSolidCamHost.ToolPath.EndToolPath(id); }
                }), "Cam/Operation", new[] { "element" }, true, ApiRefs.Cam("IToolPath.StartToolPath", "IToolPath.NextToolPathItem", "IToolPath.EndToolPath")));
        }
        private static JToken Value(object value)
        {
            if (value is TopSolid.Kernel.Automating.Point3D point) return new JObject { ["x"] = point.X, ["y"] = point.Y, ["z"] = point.Z };
            if (value is TopSolid.Kernel.Automating.Vector3D vector) return new JObject { ["x"] = vector.X, ["y"] = vector.Y, ["z"] = vector.Z };
            return value == null ? JValue.CreateNull() : JToken.FromObject(value);
        }
    }
}
