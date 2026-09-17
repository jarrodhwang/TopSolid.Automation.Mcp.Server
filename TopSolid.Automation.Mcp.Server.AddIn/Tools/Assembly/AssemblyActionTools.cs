using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class AssemblyActionTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var properties = DocumentActionTools.Target();
            properties["sourceDocumentId"] = Schema.Text("Exact part or assembly definition revision to include. Families with drivers are not supported by this tool.");
            properties["translation"] = ShapeWorkflowTools.Vector("Initial translation from definition coordinates in input units");
            properties["units"] = Schema.Choice("Translation units; default mm.", "mm", "cm", "m");
            properties["name"] = Schema.Text("Optional occurrence name.", 128);
            properties["fixed"] = Schema.Boolean("Fix the inclusion; default true.");
            register(new ToolDefinition("topsolid_include_assembly_document", "Include an existing part or assembly definition into an assembly at an explicit translation. Creates native positioning/inclusion operations. Requires confirmation; does not save.", properties,
                p => a.Modify(p, "include assembly document", "cad", (doc, current) =>
                {
                    if (!TopSolidDesignHost.Assemblies.IsAssembly(doc)) throw new ArgumentException("Target must be an assembly document.");
                    var source = a.Document(current, "sourceDocumentId");
                    if ((!TopSolidDesignHost.Parts.IsPart(source) && !TopSolidDesignHost.Assemblies.IsAssembly(source)) || TopSolidHost.Documents.GetPdmObject(source).Equals(TopSolidHost.Documents.GetPdmObject(doc)))
                        throw new ArgumentException("Choose a different part or assembly definition.");
                    var before = TopSolidDesignHost.Assemblies.GetParts(doc);
                    var inclusion = TopSolidDesignHost.Assemblies.CreateInclusion(doc, ElementId.Empty, (string)current["name"], source, null, null, null, true,
                        ElementId.Empty, ElementId.Empty, true, false, false, false, SpatialInput.Translation(current), (bool?)current["fixed"] ?? true);
                    AutomationGateway.RequireValid(inclusion, "assembly inclusion");
                    return new JObject { ["inclusion"] = AutomationValues.Json(inclusion), ["occurrences"] = AutomationValues.Json(TopSolidDesignHost.Assemblies.GetParts(doc).Except(before).ToList()) };
                }), "Assembly", new[] { "documentId", "sourceDocumentId", "translation" }, false,
                ApiRefs.For("cad", "TopSolid.Cad.Design.Automating", "IAssemblies.CreateInclusion", "IAssemblies.GetParts", "IAssemblies.IsAssembly", "IParts.IsPart"),
                MutationReferences.Validate, p => { var target = a.PreviewDocument(p, "cad"); target["definition"] = a.DocumentSummary(a.Document(p, "sourceDocumentId")); return target; },
                "Create one rigid inclusion and positioning operation in this assembly. Automatically fills representations; fixed defaults to true. Source document is referenced without modification. Does not save.", defaultLengthUnits: "mm"));
            var translate = DocumentActionTools.Target(); translate["element"] = Schema.Element(); translate["translation"] = ShapeWorkflowTools.Vector("Translation to apply in input units");
            translate["units"] = Schema.Choice("Translation units; default mm.", "mm", "cm", "m");
            register(new ToolDefinition("topsolid_translate_assembly_inclusion", "Translate an existing inclusion operation belonging to a positioning. Not for an in-place part. Use the inclusion operation ID, not a part occurrence ID. Requires confirmation.", translate,
                p => a.Modify(p, "translate assembly inclusion", "cad", (doc, current) =>
                {
                    var inclusion = a.Element(current);
                    TopSolidDesignHost.Assemblies.TransformInclusion(inclusion, SpatialInput.Translation(current));
                    AutomationGateway.RequireValid(inclusion, "assembly inclusion");
                    return new JObject { ["inclusion"] = AutomationValues.Json(inclusion), ["translationApplied"] = current["translation"].DeepClone(), ["inputUnits"] = (string)current["units"] ?? "mm" };
                }), "Assembly", new[] { "documentId", "element", "translation" }, false,
                ApiRefs.For("cad", "TopSolid.Cad.Design.Automating", "IAssemblies.TransformInclusion"), MutationReferences.Validate, p => a.PreviewDocument(p, "cad"), defaultLengthUnits: "mm"));
        }
    }
}
