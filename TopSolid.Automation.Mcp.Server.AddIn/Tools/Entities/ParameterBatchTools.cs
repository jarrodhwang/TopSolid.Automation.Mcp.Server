using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ParameterBatchTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            foreach (var create in new[] { false, true })
            {
                var isCreate = create;
                var entry = ScalarInput.Properties();
                entry["textValue"] = Schema.Text("New text value.", 512);
                if (create) entry["name"] = Schema.Text("Unique new parameter name.", 128); else entry["element"] = Schema.Element();
                var props = DocumentActionTools.Target(); props["parameters"] = Schema.Array(Schema.Object(entry, "valueType", create ? "name" : "element"), 1, BatchInput.MaximumChanges);
                register(new ToolDefinition(create ? "topsolid_create_parameters" : "topsolid_set_parameter_values",
                    (create ? "Create named scalar parameters" : "Set existing scalar parameter values") + " in one confirmed modification, up to 32 per batch. Real values are SI (metres/radians); provide UnitType. Types/units must match existing parameters. Does not save.", props,
                    p => a.Modify(p, isCreate ? "create parameters" : "set parameter values", "kernel", (doc, current) =>
                    {
                        Preflight(a, current, isCreate);
                        var result = new JArray();
                        foreach (JObject value in current["parameters"])
                        {
                            ElementId id;
                            if (isCreate)
                            {
                                switch ((string)value["valueType"])
                                {
                                    case "Real": id = TopSolidHost.Parameters.CreateRealParameter(doc, Unit(value), (double)value["realValueSI"]); break;
                                    case "Integer": id = TopSolidHost.Parameters.CreateIntegerParameter(doc, (int)value["integerValue"]); break;
                                    case "Boolean": id = TopSolidHost.Parameters.CreateBooleanParameter(doc, (bool)value["booleanValue"]); break;
                                    default: id = TopSolidHost.Parameters.CreateTextParameter(doc, (string)value["textValue"]); break;
                                }
                                AutomationGateway.RequireValid(id, "parameter"); TopSolidHost.Elements.SetName(id, (string)value["name"]);
                            }
                            else
                            {
                                id = a.Element(value);
                                switch ((string)value["valueType"])
                                {
                                    case "Real": TopSolidHost.Parameters.SetRealValue(id, (double)value["realValueSI"]); break;
                                    case "Integer": TopSolidHost.Parameters.SetIntegerValue(id, (int)value["integerValue"]); break;
                                    case "Boolean": TopSolidHost.Parameters.SetBooleanValue(id, (bool)value["booleanValue"]); break;
                                    default: TopSolidHost.Parameters.SetTextValue(id, (string)value["textValue"]); break;
                                }
                            }
                            var row = EntityBatchReadTools.Parameter(id);
                            if (isCreate && TopSolidHost.Elements.GetName(id) != (string)value["name"]) throw new InvalidOperationException("The new parameter name differs from the requested name; rolling back.");
                            var key = (string)value["valueType"] == "Real" ? "realValueSI" : (string)value["valueType"] == "Integer" ? "integerValue" : (string)value["valueType"] == "Boolean" ? "booleanValue" : "textValue";
                            var matches = key == "realValueSI" ? Math.Abs((double)row["value"] - (double)value[key]) <= Math.Max(1e-12, Math.Abs((double)value[key]) * 1e-10)
                                : JToken.DeepEquals(row["value"], value[key]);
                            if (!matches) throw new InvalidOperationException("Parameter readback differs from the requested value; rolling back the batch.");
                            result.Add(row);
                        }
                        return new JObject { ["items"] = result, ["changed"] = result.Count };
                    }), "Entities", new[] { "documentId", "parameters" }, false,
                    ApiRefs.Kernel("IParameters.CreateRealParameter", "IParameters.CreateIntegerParameter", "IParameters.CreateBooleanParameter", "IParameters.CreateTextParameter",
                        "IParameters.SetRealValue", "IParameters.SetIntegerValue", "IParameters.SetBooleanValue", "IParameters.SetTextValue", "IParameters.GetParameterType", "IParameters.GetRealUnit",
                        "IParameters.GetRealValue", "IParameters.GetIntegerValue", "IParameters.GetBooleanValue", "IParameters.GetTextValue", "IElements.SetName", "IElements.GetFriendlyName", "IElements.SearchByName", "UnitType"),
                    p => Validate(p, isCreate), p =>
                    {
                        var preview = a.PreviewDocument(p); Preflight(a, p, isCreate);
                        if (!isCreate) preview["currentValues"] = new JArray(((JArray)p["parameters"]).Cast<JObject>().Select(v => EntityBatchReadTools.Parameter(a.Element(v))));
                        return preview;
                    }));
            }
        }
        internal static UnitType Unit(JObject value)
        {
            var text = (string)value["unitType"];
            if (!Enum.GetNames(typeof(UnitType)).Contains(text) || text == "None") throw new ArgumentException("Use an exact documented UnitType, such as Length, Angle or Factor; None is not a real parameter unit.");
            return (UnitType)Enum.Parse(typeof(UnitType), text);
        }
        internal static void Validate(JObject p, bool create)
        {
            MutationReferences.Validate(p);
            BatchInput.Unique(((JArray)p["parameters"]).Select(v => v[create ? "name" : "element"]), create ? "parameter name" : "parameter");
            foreach (JObject value in p["parameters"])
            {
                ScalarInput.ValidateValue(value);
                if ((string)value["valueType"] == "Real") Unit(value);
            }
        }
        private static void Preflight(AutomationGateway a, JObject p, bool create)
        {
            var doc = a.Document(p);
            foreach (JObject value in p["parameters"])
            {
                if (create)
                {
                    if (!TopSolidHost.Elements.SearchByName(doc, (string)value["name"]).IsEmpty) throw new ArgumentException("A parameter name is already in use: " + (string)value["name"]);
                }
                else
                {
                    var id = a.Element(value); var type = TopSolidHost.Parameters.GetParameterType(id);
                    if (type.ToString() != (string)value["valueType"]) throw new ArgumentException("Parameter type does not match the requested value.");
                    if (type == ParameterType.Real)
                    {
                        TopSolidHost.Parameters.GetRealUnit(id, out var unit, out _);
                        if (unit != Unit(value)) throw new ArgumentException("Parameter unit type does not match the requested value.");
                    }
                }
            }
        }
    }
}
