using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ExplicitSectionTools
    {
        internal const string Name = "topsolid_create_sketch_section";
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var p = DocumentActionTools.Target(); p["sketch"] = Schema.Element(); p["profiles"] = Schema.Array(Schema.Item(), 1, 32);
            register(new ToolDefinition(Name,
                "Only when the user explicitly requests a sketch section: group selected existing closed profiles into one native 2D sketch section. Ordinary drawing/extrusion/revolution uses sketches directly and must never call this. Separate confirmation, no new geometry, no save.",
                p, input => a.Modify(input, "create explicitly requested sketch section", "kernel", (doc, current) => {
                    var sketch = a.Element(current, "sketch"); var profiles = Profiles(a, current); var before = TopSolidHost.Sketches2D.GetSectionCount(sketch);
                    ElementItemId section;
                    TopSolidHost.Sketches2D.StartModification(sketch);
                    try { section = TopSolidHost.Sketches2D.CreateSection(profiles); }
                    finally { TopSolidHost.Sketches2D.EndModification(); }
                    AutomationGateway.RequireValid(TopSolidHost.Sketches2D.CreateBuildingOperation(sketch), "sketch building operation");
                    var after = TopSolidHost.Sketches2D.GetSections(sketch);
                    if (section.IsEmpty || after.Count != before + 1 || !after.Contains(section)) throw new InvalidOperationException("Section creation readback differs; rolling back.");
                    var actual = TopSolidHost.Sketches2D.GetSectionProfiles(section);
                    if (actual.Count != profiles.Count || profiles.Any(id => !actual.Contains(id))) throw new InvalidOperationException("Section profiles differ from the confirmed selection; rolling back.");
                    return new JObject { ["sketch"] = AutomationValues.Json(sketch), ["section"] = AutomationValues.Json(section),
                        ["profiles"] = AutomationValues.Json(actual), ["sectionCreated"] = true, ["readBackVerified"] = true };
                }), "Sketch2D", new[] { "documentId", "sketch", "profiles" }, false,
                ApiRefs.Kernel("ISketches2D.IsSketch", "ISketches2D.GetProfiles", "ISketches2D.IsProfileClosed", "ISketches2D.StartModification", "ISketches2D.EndModification",
                    "ISketches2D.CreateSection", "ISketches2D.CreateBuildingOperation", "ISketches2D.GetSectionCount", "ISketches2D.GetSections", "ISketches2D.GetSectionProfiles"),
                input => { MutationReferences.Validate(input); BatchInput.Unique((JArray)input["profiles"], "profile");
                    if (((JArray)input["profiles"]).Any(id => !JToken.DeepEquals(id["element"], input["sketch"]))) throw new ArgumentException("Every profile must belong to the selected sketch."); },
                input => { var preview = a.PreviewDocument(input); var profiles = Profiles(a, input);
                    preview["createsExplicitSection"] = true; preview["profiles"] = AutomationValues.Json(profiles);
                    preview["existingSectionCount"] = TopSolidHost.Sketches2D.GetSectionCount(a.Element(input, "sketch")); return preview; },
                effect: "Create one explicitly requested section from the reviewed profiles. This is a separate action; ordinary sketches never create sections."));
        }
        private static List<ElementItemId> Profiles(AutomationGateway a, JObject p)
        {
            var sketch = a.Element(p, "sketch");
            var profiles = ((JArray)p["profiles"]).Select(id => a.Item(new JObject { ["item"] = id.DeepClone() })).ToList();
            ValidateProfiles(TopSolidHost.Sketches2D, sketch, profiles); return profiles;
        }
        internal static void ValidateProfiles(ISketches2D api, ElementId sketch, List<ElementItemId> profiles)
        {
            if (!api.IsSketch(sketch)) throw new ArgumentException("An existing native 2D sketch is required.");
            var known = api.GetProfiles(sketch);
            if (profiles.Count == 0 || profiles.Count > 32 || profiles.Distinct().Count() != profiles.Count || profiles.Any(id => !id.ElementId.Equals(sketch) || !known.Contains(id) || !api.IsProfileClosed(id)))
                throw new ArgumentException("Choose distinct existing closed profile handles from this sketch. Do not fabricate profiles or create sections for ordinary drawing/modeling.");
        }
    }
}
