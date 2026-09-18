using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;
using CamParameters = TopSolid.Cam.NC.Kernel.Automating.IParameters;
using CamParameterType = TopSolid.Cam.NC.Kernel.Automating.ParameterType;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void CamParameters()
        {
            var toolFields = new Dictionary<string, string> { ["ToolDefinitionName"] = " Ball Nose Mill D10 L25 SD10 ",
                ["PocketDescription"] = "T 1", ["ToolFunction"] = "BallNoseMill" };
            var toolDisplay = CamToolPresentation.FromValues(toolFields);
            Check((string)toolDisplay["toolDisplayName"] == "T 1 : Ball Nose Mill D10 L25 SD10", "Native tool pocket/specification formatting failed");
            toolFields.Remove("PocketDescription");
            Check((string)CamToolPresentation.FromValues(toolFields)["toolDisplayName"] == "Ball Nose Mill D10 L25 SD10", "Missing pocket invented a tool number");
            toolFields.Clear();
            Check(CamToolPresentation.FromValues(toolFields)["toolDisplayName"].Type == JTokenType.Null, "Missing tool metadata invented a cutter definition");
            var operation = new ElementExId(new ElementId(new DocumentId("cam-revision"), 12788));
            var id = new ParameterId(operation, "CuttingSpeed@CuttingConditions");
            var type = CamParameterType.Real; SmartObject stored = new SmartReal(UnitType.Velocity, 2);
            var readOnly = false; var enumName = ""; var refuseChange = false; var failMetadata = false; var failValue = false;
            var choices = new List<string> { "Off", "__", "Flood", "", "Mist" };
            var all = new List<ParameterId> { id };
            var proxy = new ContractProxy<CamParameters>((c, args) => {
                switch (c.MethodName) {
                    case "GetParameters": return all;
                    case "GetType": return type;
                    case "GetValue": if (failValue) throw new InvalidOperationException("Generic value unavailable"); return stored;
                    case "GetName": return "Cutting speed";
                    case "GetFullName": return ((ParameterId)args[0]).Name;
                    case "GetLocalizedName": return "절삭 속도";
                    case "GetCategories": return new[] { "CuttingConditions" };
                    case "ToStringValue": return "120 m/min";
                    case "ToInvariantStringValue": return "120m/min";
                    case "IsReadOnly": if (failMetadata) throw new InvalidOperationException("Metadata unavailable"); return readOnly;
                    case "GetEnumTypeName": return enumName;
                    case "GetParameterEnumValueNames": return choices;
                    case "GetValueFeedRateValue": return new SmartReal(UnitType.Velocity, .001);
                    case "GetValueSpindleRateValue": return new SmartReal(UnitType.AngularVelocity, 100);
                    case "GetValueBoundValue": return new SmartReal(UnitType.Length, .025);
                    case "GetValueBoundElement": return ElementId.Empty;
                    case "SetValue": if (refuseChange) return false; stored = (SmartObject)args[1]; return true;
                    default: throw new NotSupportedException(c.MethodName);
                }
            });
            var access = new CamParameterValues(proxy.Interface, _ => "Face milling");
            var row = access.Read(id);
            Check((string)row["unitType"] == "Velocity" && (double)row["realValueSI"] == 2 && (string)row["displayName"] == "절삭 속도", "CAM values lost real units or friendly names");
            Check((bool)row["editSupported"] && (string)row["parameter"]["name"] == "CuttingSpeed@CuttingConditions", "CAM identity must preserve exact category name");
            var request = new JObject { ["valueType"] = "Real", ["unitType"] = "Length", ["realValueSI"] = .0666666666667 };
            var failure = Throws<ArgumentException>(() => access.Preflight(id, request, out _));
            Check(failure.Message.Contains("Velocity") && !proxy.Calls.Contains("SetValue"), "Wrong cutting-speed Length unit must fail before any native setter or approval");
            request["unitType"] = "Velocity";
            var changed = access.Set(id, request);
            Check((bool)changed["readBackVerified"] && (bool)changed["changed"] && ((SmartReal)stored).UnitType == UnitType.Velocity, "Typed CAM change lacks verified readback");
            // Equal computed values do not prove the approved literal replacement occurred.
            refuseChange = true;
            stored = new SmartReal(UnitType.Velocity, (double)request["realValueSI"]) { Type = SmartRealType.Formula, Formula = "NominalCuttingSpeed" };
            var retainedFormula = Throws<InvalidOperationException>(() => access.Set(id, request));
            Check(retainedFormula.Message.Contains("formula/reference"), "Matching computed value must not hide a refused literal replacement");
            stored = new SmartReal(UnitType.Velocity, (double)request["realValueSI"]);
            refuseChange = true; request["realValueSI"] = 3;
            Throws<InvalidOperationException>(() => access.Set(id, request));
            var end = new List<bool>();
            Throws<InvalidOperationException>(() => ModificationScope.Run("CAM mismatch", _ => true, (commit, update) => end.Add(commit), () => access.Set(id, request)));
            Check(end.SequenceEqual(new[] { false }), "Native refusal must roll back the CAM transaction");
            refuseChange = false; readOnly = true;
            var beforeSets = proxy.Calls.Count(c => c == "SetValue"); Throws<ArgumentException>(() => access.Set(id, request));
            Check(proxy.Calls.Count(c => c == "SetValue") == beforeSets, "Read-only CAM parameter was sent to setter");
            readOnly = false; failMetadata = true; row = access.Read(id);
            Check((bool?)row["editSupported"] == false && row["metadataErrors"]["readOnly"] != null && row["realValueSI"] != null, "Partial metadata failure must retain value but disable editing");
            Throws<ArgumentException>(() => access.Preflight(id, request, out _)); failMetadata = false;
            stored = new SmartReal(UnitType.None, 2);
            Check(!(bool)access.Read(id)["editSupported"], "Unknown units must never be inferred from CuttingSpeed name");
            Throws<ArgumentException>(() => access.Preflight(id, request, out _));
            type = CamParameterType.Integer; stored = new SmartInteger(2); enumName = "CoolantMode";
            row = access.Read(id); var allowed = (JArray)row["allowedValues"];
            Check(allowed.Select(c => (int)c["value"]).SequenceEqual(new[] { 0, 2, 4 }), "Excluded enumeration holes were renumbered or exposed");
            request = new JObject { ["valueType"] = "Integer", ["integerValue"] = 1 };
            Throws<ArgumentException>(() => access.Preflight(id, request, out _));
            request["integerValue"] = 4; access.Set(id, request);
            Check(((SmartInteger)stored).Value == 4, "Native enum value index was not preserved");
            Throws<InvalidOperationException>(() => CamParameterValues.Choices(choices, new List<string> { "Off" }));
            enumName = "";
            failValue = true; type = CamParameterType.FeedRate; row = access.Read(id);
            Check(row["metadataErrors"]["value"] != null && (string)row["feedRateValue"]["unitType"] == "Velocity" && (string)row["displayName"] == "절삭 속도", "A failed generic value must not hide composite/name metadata");
            failValue = false;
            foreach (var composite in new[] { CamParameterType.FeedRate, CamParameterType.SpindleRate, CamParameterType.Bound, CamParameterType.Tool, CamParameterType.Geometry, CamParameterType.Element, CamParameterType.AxisPosition, CamParameterType.Untyped, CamParameterType.Unclassified }) {
                type = composite; stored = new SmartInteger(1); row = access.Read(id);
                Check(!(bool)row["editSupported"], "Unsupported composite gained a setter: " + composite);
                if (composite == CamParameterType.FeedRate) Check((string)row["feedRateValue"]["unitType"] == "Velocity", "FeedRate must read its dedicated typed value");
                if (composite == CamParameterType.SpindleRate) Check((string)row["spindleRateValue"]["unitType"] == "AngularVelocity", "SpindleRate must read its dedicated typed value");
                if (composite == CamParameterType.Bound) Check((double)row["boundValue"]["realValueSI"] == .025 && row["boundElement"].Type == JTokenType.Null, "Bound must read value and element separately");
            }
            foreach (var scalar in new[] { CamParameterType.Boolean, CamParameterType.Text }) {
                type = scalar; stored = scalar == CamParameterType.Boolean ? (SmartObject)new SmartBoolean(true) : new SmartText("M8");
                request = scalar == CamParameterType.Boolean ? new JObject { ["valueType"] = "Boolean", ["booleanValue"] = false } : new JObject { ["valueType"] = "Text", ["textValue"] = "M9" };
                Check((bool)access.Set(id, request)["readBackVerified"], "Scalar CAM readback failed: " + scalar);
            }
            all = new List<ParameterId> { new ParameterId(operation, "LeadRadius@Lead|LeadIn"), new ParameterId(operation, "LeadRadius@Lead|LeadOut") };
            Throws<ArgumentException>(() => access.Resolve(operation, "LeadRadius"));
            Check(access.Resolve(operation, "LeadRadius@Lead|LeadOut").Name == "LeadRadius@Lead|LeadOut", "Exact CAM category resolution changed");
            all.Add(all[1]); Throws<ArgumentException>(() => access.Resolve(operation, all[1].Name));
            type = CamParameterType.Real; stored = new SmartReal(UnitType.Length, .025);
            all = Enumerable.Range(0, 637).Select(i => new ParameterId(operation, "Parameter" + i + (i % 2 == 0 ? "@CuttingConditions" : "@Strategy"))).ToList();
            var offset = 0; var seen = new HashSet<string>(); var pages = 0;
            do {
                var page = access.Page(operation, new JObject { ["offset"] = offset, ["limit"] = 100 }); pages++;
                Check(page.ToString(Formatting.None).Length < 50000 && (int)page["failed"] == 0, "CAM catalog exceeded model result budget");
                foreach (var item in (JArray)page["items"]) Check(seen.Add((string)item["name"]), "CAM paging duplicated a parameter");
                if (!(bool)page["hasMore"]) break;
                Check((int)page["nextOffset"] > offset, "CAM paging did not advance"); offset = (int)page["nextOffset"];
            } while (pages < 24);
            Check(seen.Count == 637 && pages < 24, "Complete CAM inventory cannot fit the assistant tool loop");
            var filtered = access.Page(operation, new JObject { ["category"] = "CuttingConditions", ["nameContains"] = "Parameter6", ["limit"] = 100 });
            Check((int)filtered["totalOperationParameters"] == 637 && ((JArray)filtered["items"]).All(r => ((JArray)r["categories"]).Any(c => (string)c == "CuttingConditions")), "Native category filtering lost operation total or mixed categories");
            using (var gateway = new AutomationGateway()) {
                var registry = new ToolRegistry(gateway); var tools = registry.List();
                var catalog = tools.Single(t => (string)t["name"] == "topsolid_list_cam_parameters");
                Check((int)catalog["inputSchema"]["properties"]["limit"]["default"] == 100, "CAM inventory default page must be 100");
                var write = new JObject { ["documentId"] = "cam-revision", ["element"] = new JObject { ["documentId"] = "cam-revision", ["id"] = 12788 }, ["name"] = id.Name, ["valueType"] = "Real", ["unitType"] = "Velocity", ["realValueSI"] = 2 };
                Check(Throws<RpcException>(() => registry.Call("topsolid_set_cam_parameter_value", write)).Code == -32010, "CAM setter bypassed confirmation");
            }
        }
    }
}
