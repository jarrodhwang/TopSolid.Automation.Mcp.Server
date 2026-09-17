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
    internal static class Sketch3DReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_sketches3d", "List sketches3d.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches3D.GetSketches(a.Document(p)), p)), "Sketch3D", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches3D.GetSketches.html" }));
            register(new ToolDefinition("topsolid_list_sketch3d_profiles", "List sketch3d profiles.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches3D.GetProfiles(a.Element(p)), p)), "Sketch3D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches3D.GetProfiles.html" }));
            register(new ToolDefinition("topsolid_list_sketch3d_segments", "List sketch3d segments.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches3D.GetSegments(a.Element(p)), p)), "Sketch3D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches3D.GetSegments.html" }));
            register(new ToolDefinition("topsolid_list_sketch3d_vertices", "List sketch3d vertices.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches3D.GetVertices(a.Element(p)), p)), "Sketch3D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches3D.GetVertices.html" }));
            register(new ToolDefinition("topsolid_get_sketch3d_vertex_point", "Get sketch3d vertex point.", new JObject { ["item"] = Schema.Item() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Sketches3D.GetVertexPoint(a.Item(p)))), "Sketch3D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches3D.GetVertexPoint.html" }));
            register(new ToolDefinition("topsolid_get_sketch3d_profile_segments", "Get sketch3d profile segments.", Schema.Page(new JObject { ["item"] = Schema.Item() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches3D.GetProfileSegments(a.Item(p)), p)), "Sketch3D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches3D.GetProfileSegments.html" }));
            register(new ToolDefinition("topsolid_get_sketch3d_profile_closed", "Get sketch3d profile closed.", new JObject { ["item"] = Schema.Item() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Sketches3D.IsProfileClosed(a.Item(p)))), "Sketch3D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches3D.IsProfileClosed.html" }));
            register(new ToolDefinition("topsolid_get_sketch3d_segment_curve_type", "Get sketch3d segment curve type.", new JObject { ["item"] = Schema.Item() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Sketches3D.GetSegmentCurveType(a.Item(p)))), "Sketch3D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches3D.GetSegmentCurveType.html" }));
        }
    }
}
