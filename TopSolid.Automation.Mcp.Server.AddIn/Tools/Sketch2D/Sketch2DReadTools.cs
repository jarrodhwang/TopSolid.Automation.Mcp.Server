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
    internal static class Sketch2DReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_sketches2d", "List sketches2d.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches2D.GetSketches(a.Document(p)), p)), "Sketch2D", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetSketches.html" }));
            register(new ToolDefinition("topsolid_list_sketch2d_profiles", "List sketch2d profiles.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches2D.GetProfiles(a.Element(p)), p)), "Sketch2D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetProfiles.html" }));
            register(new ToolDefinition("topsolid_list_sketch2d_segments", "List sketch2d segments.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches2D.GetSegments(a.Element(p)), p)), "Sketch2D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetSegments.html" }));
            register(new ToolDefinition("topsolid_list_sketch2d_vertices", "List sketch2d vertices.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches2D.GetVertices(a.Element(p)), p)), "Sketch2D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetVertices.html" }));
            register(new ToolDefinition("topsolid_get_sketch2d_vertex_point", "Get sketch2d vertex point.", new JObject { ["item"] = Schema.Item() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Sketches2D.GetVertexPoint(a.Item(p)))), "Sketch2D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetVertexPoint.html" }));
            register(new ToolDefinition("topsolid_get_sketch2d_profile_segments", "Get sketch2d profile segments.", Schema.Page(new JObject { ["item"] = Schema.Item() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches2D.GetProfileSegments(a.Item(p)), p)), "Sketch2D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetProfileSegments.html" }));
            register(new ToolDefinition("topsolid_get_sketch2d_profile_closed", "Get sketch2d profile closed.", new JObject { ["item"] = Schema.Item() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Sketches2D.IsProfileClosed(a.Item(p)))), "Sketch2D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.IsProfileClosed.html" }));
            register(new ToolDefinition("topsolid_get_sketch2d_segment_curve_type", "Get sketch2d segment curve type.", new JObject { ["item"] = Schema.Item() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Sketches2D.GetSegmentCurveType(a.Item(p)))), "Sketch2D", new[] { "item" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetSegmentCurveType.html" }));
        }
    }
}
