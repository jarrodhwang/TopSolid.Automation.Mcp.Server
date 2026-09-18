using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ParameterBatchTools
    {
        internal static readonly string[] WriteApi = ParameterValues.ReadApi.Concat(ParameterValues.ChoicesApi).Concat(ParameterValues.ConstraintApi).Concat(ApiRefs.Kernel(
            "IParameters.CreateRealParameter", "IParameters.CreateIntegerParameter", "IParameters.CreateBooleanParameter", "IParameters.CreateTextParameter", "IParameters.CreateColorParameter", "IParameters.CreateDateTimeParameter", "IParameters.CreateToleranceParameter", "IParameters.CreateUserEnumParameter",
            "IParameters.SetRealValue", "IParameters.SetIntegerValue", "IParameters.SetBooleanValue", "IParameters.SetTextValue", "IParameters.SetDateTimeValue", "IParameters.SetColorValue", "IParameters.SetTolerance", "IParameters.SetEnumerationValue", "IParameters.SetUserEnumerationValue", "IParameters.SetCodeValue", "IParameters.SetFamilyValue", "IElements.SetName", "IElements.SearchByName", "IDocuments.Exists", "IElements.IsInvalid", "IUnits.GetUnitSymbol", "UnitType")).ToArray();
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            foreach (var create in new[] { false, true }) {
                var isCreate = create; var entry = ParameterValueInput.Properties(create);
                entry[create ? "name" : "element"] = create ? Schema.Text("Requested new parameter name. Collisions receive _1, _2, etc.; use the assigned name and handle returned by this tool.", 128) : Schema.Element();
                var props = DocumentActionTools.Target(); props["parameters"] = Schema.Array(Schema.Object(entry, "valueType", create ? "name" : "element"), 1, BatchInput.MaximumChanges);
                register(new ToolDefinition(create ? "topsolid_create_parameters" : "topsolid_set_parameter_values",
                    (create ? "Create named typed parameters (including Color, DateTime, Tolerance and UserEnumeration)" : "Set typed literal parameter values, including enumerations, Code and Family") + " in one confirmed batch of up to 32. Real/tolerance values are SI. Linked/driven parameters use expression/source tools. Preflight and readback; no save.", props,
                    p => Execute(a, p, isCreate), "Parameters", new[] { "documentId", "parameters" }, false, WriteApi,
                    p => Validate(p, isCreate), p => Preview(a, p, isCreate)));
            }
            var single = DocumentActionTools.Target(); single["element"] = Schema.Element(); single.Merge(ParameterValueInput.Properties(false));
            register(new ToolDefinition("topsolid_set_parameter_value", "Set one typed literal parameter through the same checked batch adapter. Prefer set_parameter_values for multiple changes. Refuses driven/relayed definitions; no save.", single,
                p => Execute(a, AsBatch(p), false), "Parameters", new[] { "documentId", "element", "valueType" }, false, WriteApi,
                p => Validate(AsBatch(p), false), p => Preview(a, AsBatch(p), false)));
        }
        private static JObject AsBatch(JObject p) { var entry = (JObject)p.DeepClone(); entry.Remove("documentId"); return new JObject { ["documentId"] = p["documentId"].DeepClone(), ["parameters"] = new JArray(entry) }; }
        internal static UnitType Unit(JObject value) => ParameterValueInput.Unit(value);
        internal static void Validate(JObject p, bool create)
        {
            MutationReferences.Validate(p);
            if (!create) BatchInput.Unique(((JArray)p["parameters"]).Select(v => v["element"]), "parameter");
            foreach (JObject value in p["parameters"]) ParameterValueInput.Validate(value, create);
        }
        private static void Preflight(AutomationGateway a, JObject p, bool create)
        {
            var doc = a.Document(p); var access = ParameterValues.Native;
            foreach (JObject value in p["parameters"]) {
                if (create) {
                    if (!TopSolidHost.Elements.SearchByName(doc, (string)value["name"]).IsEmpty) throw new ArgumentException("A parameter name is already in use: " + (string)value["name"]);
                    if ((string)value["valueType"] == "UserEnumeration") {
                        var definition = a.Document(new JObject { ["documentId"] = value["definitionDocumentId"].DeepClone() });
                        TopSolidHost.Parameters.GetUserEnumerationValues(definition, out var values, out _);
                        if (!values.Contains((int)value["enumerationValue"])) throw new ArgumentException("The enumeration key is not in the selected native definition.");
                    }
                } else {
                    access.Preflight(a.Element(value), value);
                    if ((string)value["valueType"] == "Family") a.Document(new JObject { ["documentId"] = value["familyDocumentId"].DeepClone() });
                }
            }
        }
        private static JObject Preview(AutomationGateway a, JObject p, bool create)
        {
            var preview = a.PreviewDocument(p); Preflight(a, p, create);
            if (!create) preview["currentValues"] = new JArray(((JArray)p["parameters"]).Cast<JObject>().Select(v => ParameterValues.Native.Read(a.Element(v), true, true)));
            return preview;
        }
        private static JObject Execute(AutomationGateway a, JObject p, bool create) => a.Modify(p, create ? "create parameters" : "set parameter values", "kernel", (doc, current) => {
            Preflight(a, current, create); var result = new JArray(); var access = ParameterValues.Native;
            foreach (JObject value in current["parameters"]) {
                var id = create ? access.Create(doc, value) : a.Element(value);
                AutomationGateway.RequireValid(id, "parameter");
                if (create) CreationNames.SetAndVerify(TopSolidHost.Elements, id, (string)value["name"]); else access.Set(id, value);
                var row = access.Read(id); row["name"] = TopSolidHost.Elements.GetName(id); row["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(id);
                if (create && (string)row["name"] != (string)value["name"]) throw new InvalidOperationException("Parameter name readback differs; rolling back.");
                ParameterValues.Verify(row, value); result.Add(row);
            }
            return new JObject { ["items"] = result, ["changed"] = result.Count };
        });
    }
}
