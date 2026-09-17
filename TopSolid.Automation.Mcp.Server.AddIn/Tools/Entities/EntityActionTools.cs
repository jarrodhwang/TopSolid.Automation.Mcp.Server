using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ScalarInput
    {
        public static JObject Properties()
        {
            return new JObject { ["valueType"] = Schema.Choice("Exact scalar type from a live parameter query.", "Real", "Integer", "Boolean", "Text"),
                ["realValueSI"] = Schema.Number("New real value in SI units, such as metres or radians.", -1e12, 1e12), ["unitType"] = Schema.Text("Exact real UnitType from the live parameter query.", 128),
                ["integerValue"] = Schema.Integer("New integer value.", int.MinValue), ["booleanValue"] = Schema.Boolean("New Boolean value."), ["textValue"] = Schema.Text("New text value.", 4096) };
        }
        public static void Validate(JObject p)
        {
            MutationReferences.Validate(p);
            ValidateValue(p);
        }
        internal static void ValidateValue(JObject p)
        {
            var type = (string)p["valueType"];
            var key = type == "Real" ? "realValueSI" : type == "Integer" ? "integerValue" : type == "Boolean" ? "booleanValue" : "textValue";
            foreach (var field in new[] { "realValueSI", "integerValue", "booleanValue", "textValue" })
                if ((p[field] != null) != (field == key)) throw new ArgumentException("Provide exactly the value field matching valueType.");
            if ((p["unitType"] != null) != (type == "Real")) throw new ArgumentException("unitType is required only for a real value, to prevent unit mistakes.");
        }
    }
    internal static class EntityActionTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            foreach (var kind in new[] { "rename", "visibility", "translate" })
            {
                var action = kind; var p = DocumentActionTools.Target(); p["element"] = Schema.Element();
                var valueKey = kind == "rename" ? "name" : kind == "visibility" ? "visible" : "translation";
                p[valueKey] = kind == "rename" ? Schema.Text("New element name.", 128) : kind == "visibility" ? Schema.Boolean("Show when true, hide when false.") : ShapeWorkflowTools.Vector("Translation in input length units");
                if (kind == "translate") p["units"] = Schema.Choice("Translation length units; default mm.", "mm", "cm", "m");
                register(new ToolDefinition(kind == "visibility" ? "topsolid_set_element_visibility" : "topsolid_" + kind + "_element",
                    kind + " an existing element in an undoable modification. Requires explicit confirmation; does not save.", p,
                    input => a.Modify(input, action + " element", "kernel", (doc, current) =>
                    {
                        var element = a.Element(current);
                        var result = new JObject { ["element"] = AutomationValues.Json(element) };
                        if (action == "rename") { ObjectIdentity.ValidateRename(element, (string)current["name"]); TopSolidHost.Elements.SetName(element, (string)current["name"]); result["name"] = TopSolidHost.Elements.GetName(element);
                            if ((string)result["name"] != (string)current["name"]) throw new InvalidOperationException("Element name readback differs; rolling back."); result["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(element); }
                        else if (action == "visibility") { if ((bool)current["visible"]) TopSolidHost.Elements.Show(element); else TopSolidHost.Elements.Hide(element); result["visible"] = TopSolidHost.Elements.IsVisible(element); }
                        else { ObjectIdentity.RequireEntity(element); var operation = TopSolidHost.Entities.Transform(element, SpatialInput.Translation(current)); AutomationGateway.RequireValid(operation, "transform operation"); result["operation"] = AutomationValues.Json(operation); }
                        return result;
                    }), "Entities", new[] { "documentId", "element", valueKey }, false,
                    kind == "rename" ? ApiRefs.Kernel("IElements.SetName", "IElements.GetName", "IElements.GetFriendlyName", "IElements.IsRenamable", "IElements.HasSystemName", "IElements.HasUniqueName", "IElements.SearchByName") : kind == "visibility" ? ApiRefs.Kernel("IElements.Show", "IElements.Hide", "IElements.IsVisible") : ApiRefs.Kernel("IEntities.IsEntity", "IEntities.Transform", "Transform3D.SetTranslation"),
                    MutationReferences.Validate, input => { var target = a.PreviewDocument(input); var element = a.Element(input);
                        if (action == "rename") ObjectIdentity.ValidateRename(element, (string)input["name"]);
                        if (action == "translate") ObjectIdentity.RequireEntity(element);
                        target["elementName"] = TopSolidHost.Elements.GetFriendlyName(element); target["internalName"] = TopSolidHost.Elements.GetName(element); return target; }, defaultLengthUnits: kind == "translate" ? "mm" : null));
            }
            var parameters = DocumentActionTools.Target(); parameters["element"] = Schema.Element(); parameters.Merge(ScalarInput.Properties());
            register(new ToolDefinition("topsolid_set_parameter_value", "Set an existing scalar Design/document parameter. Query its type and units first. Real values are SI. Requires confirmation; dependent geometry updates on commit. Does not save.",
                parameters, p => a.Modify(p, "set parameter", "kernel", (doc, current) =>
                {
                    var element = a.Element(current);
                    var type = TopSolidHost.Parameters.GetParameterType(element);
                    if (type.ToString() != (string)current["valueType"]) throw new ArgumentException("Parameter type changed or does not match valueType.");
                    switch (type)
                    {
                        case ParameterType.Real:
                            TopSolidHost.Parameters.GetRealUnit(element, out var unit, out _);
                            if (unit.ToString() != (string)current["unitType"]) throw new ArgumentException("Real unit type does not match this parameter.");
                            TopSolidHost.Parameters.SetRealValue(element, (double)current["realValueSI"]); break;
                        case ParameterType.Integer: TopSolidHost.Parameters.SetIntegerValue(element, (int)current["integerValue"]); break;
                        case ParameterType.Boolean: TopSolidHost.Parameters.SetBooleanValue(element, (bool)current["booleanValue"]); break;
                        case ParameterType.Text: TopSolidHost.Parameters.SetTextValue(element, (string)current["textValue"]); break;
                        default: throw new ArgumentException("Only scalar Real, Integer, Boolean and Text parameters can be modified.");
                    }
                    return EntityDetailsTools.Value(element);
                }), "Entities", new[] { "documentId", "element", "valueType" }, false,
                ApiRefs.Kernel("IParameters.SetRealValue", "IParameters.SetIntegerValue", "IParameters.SetBooleanValue", "IParameters.SetTextValue", "IParameters.GetRealUnit"),
                ScalarInput.Validate, p => { var target = a.PreviewDocument(p); target["currentParameter"] = EntityDetailsTools.Value(a.Element(p)); return target; },
                "Replace this scalar parameter value and update its dependent geometry in one undoable modification. SI units for real values. Does not save."));
            register(new ToolDefinition("topsolid_get_user_selection", "Read the single entity and single operation currently selected by the user in TopSolid. Empty means none or multiple; never infer an ID.", new JObject(),
                p => a.Read("kernel", () => new JObject { ["entity"] = AutomationValues.Json(TopSolidHost.User.SelectedEntity), ["operation"] = AutomationValues.Json(TopSolidHost.User.SelectedOperation) }),
                "Entities", api: ApiRefs.Kernel("IUser.SelectedEntity", "IUser.SelectedOperation")));
            register(new ToolDefinition("topsolid_list_modeling_operations", "List document operations in execution order, with names, types and invalid state. These are Design/kernel operations, not the CAM operation list.",
                Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision; default active.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Operations.GetOperations(a.Document(p)), p,
                    id => new JObject { ["element"] = AutomationValues.Json(id), ["name"] = TopSolidHost.Elements.GetFriendlyName(id), ["type"] = TopSolidHost.Elements.GetTypeFullName(id), ["invalid"] = TopSolidHost.Elements.IsInvalid(id) })),
                "Entities", api: ApiRefs.Kernel("IOperations.GetOperations", "IElements.IsInvalid", "IElements.GetFriendlyName", "IElements.GetTypeFullName")));
        }
    }
}
