using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private sealed class ContractProxy<T> : RealProxy
        {
            internal readonly List<string> Calls = new List<string>();
            internal Func<IMethodCallMessage, object[], object> Handle;
            internal ContractProxy(Func<IMethodCallMessage, object[], object> handle) : base(typeof(T)) { Handle = handle; }
            internal T Interface => (T)GetTransparentProxy();
            public override IMessage Invoke(IMessage message)
            {
                var call = (IMethodCallMessage)message; Calls.Add(call.MethodName);
                try { var args = call.Args; var value = Handle(call, args); return new ReturnMessage(value, args, args.Length, call.LogicalCallContext, call); }
                catch (Exception ex) { return new ReturnMessage(ex, call); }
            }
        }
        private static void ParameterEntities()
        {
            var id = new ElementId(new DocumentId("rev"), 1); var parent = new ElementId(id.DocumentId, 2);
            JToken Handle(int number) => new JObject { ["documentId"] = "rev", ["id"] = number };
            var values = new Dictionary<string, JObject> {
                ["Real"] = new JObject { ["realValueSI"] = 0.012, ["unitType"] = "Length" }, ["Integer"] = new JObject { ["integerValue"] = -7 },
                ["Boolean"] = new JObject { ["booleanValue"] = false }, ["Text"] = new JObject { ["textValue"] = "" },
                ["DateTime"] = new JObject { ["dateTimeValue"] = "2026-09-17T12:30:00.125" }, ["Color"] = JObject.Parse("{colorValue:{r:23,g:80,b:255}}"),
                ["Tolerance"] = JObject.Parse("{unitType:'Length',lowerDeviationSI:-0.0001,upperDeviationSI:0.0002}"),
                ["Enumeration"] = new JObject { ["enumerationValue"] = 107 }, ["UserEnumeration"] = new JObject { ["enumerationValue"] = 107 },
                ["Code"] = new JObject { ["codeValue"] = "M6" }, ["Family"] = new JObject { ["familyDocumentId"] = "family-revision" }
            };
            var kinds = values.Keys.ToArray(); var nativeType = ParameterType.Real; var hasValue = true; var linked = false; var driven = false; var editable = true; var specialText = false;
            object storedValue = 0.012;
            var ep = new ContractProxy<IElements>((c, args) => c.MethodName == "GetParent" ? (object)(driven ? parent : ElementId.Empty) : c.MethodName == "IsModifiable" ? editable : c.MethodName == "IsInvalid" ? false : c.MethodName == "GetName" ? "Diameter" : c.MethodName == "GetFriendlyName" ? "직경" : throw new NotSupportedException(c.MethodName));
            var pp = new ContractProxy<IParameters>((c, args) => {
                switch (c.MethodName) {
                    case "GetParameterType": return nativeType;
                    case "HasValue": return hasValue;
                    case "GetRelayType": return linked ? ParameterRelayType.Internal : ParameterRelayType.None;
                    case "GetRelayedParameter": return parent;
                    case "GetRealUnit": args[1] = UnitType.Length; args[2] = "mm"; return null;
                    case "GetRealValue": case "GetIntegerValue": case "GetBooleanValue": case "GetTextValue": case "GetDateTimeValue": case "GetColorValue": case "GetCodeValue": case "GetFamilyValue": case "GetEnumerationValue": case "GetUserEnumerationValue": return storedValue;
                    case "IsTextLocalized": case "IsTextParameterized": return specialText;
                    case "GetTextLocalizedValue": return "Localized";
                    case "GetTextParameterizedValue": return "[$Code]";
                    case "GetEnumerationDefinition": return Guid.Parse("a45607d0-e4f8-47a3-a685-218dfebd32c7");
                    case "GetUserEnumerationDefinition": return new DocumentId("enum-revision");
                    case "GetEnumerationText": case "GetUserEnumerationText": return "Choice 107";
                    case "GetToleranceUnit": return UnitType.Length;
                    case "GetToleranceLowerDeviation": return new Real(UnitType.Length, "mm", -0.0001);
                    case "GetToleranceUpperDeviation": return new Real(UnitType.Length, "mm", 0.0002);
                    case "GetToleranceDefinition": return DocumentId.Empty;
                    case "GetToleranceClass": return "";
                    case "HasRealParameterConstraints": case "HasRealParameterPossibleValues": case "HasColorParameterPossibleValues": case "HasCodePossibleValues": case "HasUserEnumerationPossibleValues": return false;
                    case "GetEnumerationValues": case "GetUserEnumerationValues": args[1] = Enumerable.Range(0, 140).Select(i => i * 10 + 7).ToList(); args[2] = Enumerable.Range(0, 140).Select(i => "Choice " + i).ToList(); return null;
                    default: if (c.MethodName.StartsWith("Create")) return id; if (c.MethodName.StartsWith("Set")) return null; throw new NotSupportedException(c.MethodName);
                }
            });
            var access = new ParameterValues(pp.Interface, ep.Interface, _ => "mm");
            var nativeValues = new object[] { .012, -7, false, "", new DateTime(2026, 9, 17, 12, 30, 0).AddMilliseconds(125), new Color(23, 80, 255), null, 107, 107, "M6", new DocumentId("family-revision") };
            using (var gateway = new AutomationGateway()) {
                var registry = new ToolRegistry(gateway); var catalog = registry.List();
                JObject Shape(string name) => (JObject)catalog.Single(t => (string)t["name"] == name)["inputSchema"];
                foreach (var kind in kinds) {
                    var expected = (JObject)values[kind].DeepClone(); expected["valueType"] = kind; expected["element"] = Handle(1);
                    var args = new JObject { ["documentId"] = "rev", ["parameters"] = new JArray(expected) };
                    Schema.Validate(args, Shape("topsolid_set_parameter_values")); ParameterBatchTools.Validate(args, false);
                    Check(Throws<RpcException>(() => registry.Call("topsolid_set_parameter_values", args)).Code == -32010, "Typed value bypassed confirmation: " + kind);
                    nativeType = (ParameterType)Enum.Parse(typeof(ParameterType), kind); storedValue = nativeValues[Array.IndexOf(kinds, kind)];
                    var read = access.Read(id, true); Check((string)read["type"] != "Real" || (string)read["unitType"] == "Length", "Contract proxy failed out-parameter unit: " + read); ParameterValues.Verify(read, expected);
                    Check((bool)read["readBackVerified"] && (string)read["friendlyName"] == "직경", "Typed readback/name mismatch for " + kind);
                    access.Preflight(id, expected); var before = pp.Calls.Count; access.Set(id, expected);
                    Check(pp.Calls.Count == before + 1 && pp.Calls.Last().StartsWith("Set"), "Typed set must dispatch one native setter: " + kind);
                    var bad = (JObject)read.DeepClone(); bad["hasValue"] = false; Throws<InvalidOperationException>(() => ParameterValues.Verify(bad, expected));
                    if (ParameterValueInput.CreateTypes.Contains(kind)) {
                        var create = (JObject)expected.DeepClone(); create.Remove("element"); create["name"] = "User parameter"; if (kind == "UserEnumeration") create["definitionDocumentId"] = "enum-revision";
                        var createArgs = new JObject { ["documentId"] = "rev", ["parameters"] = new JArray(create) };
                        Schema.Validate(createArgs, Shape("topsolid_create_parameters")); ParameterBatchTools.Validate(createArgs, true);
                        Check(access.Create(id.DocumentId, create).Equals(id), "Creation did not return the parameter entity: " + kind);
                        Check(Throws<RpcException>(() => registry.Call("topsolid_create_parameters", createArgs)).Code == -32010, "Creation bypassed confirmation: " + kind);
                    }
                    var single = (JObject)expected.DeepClone(); single["documentId"] = "rev"; Schema.Validate(single, Shape("topsolid_set_parameter_value"));
                    Check(Throws<RpcException>(() => registry.Call("topsolid_set_parameter_value", single)).Code == -32010, "Single setter bypassed shared checks: " + kind);
                }
                var expression = JObject.Parse("{documentId:'rev',parameters:[{name:'Double diameter',valueType:'Real',mode:'formula',unitType:'Length',formula:'2 * Diameter'}]}");
                Schema.Validate(expression, Shape("topsolid_create_parameter_expressions")); ParameterExpressionTools.Validate(expression, true);
                Check(Throws<RpcException>(() => registry.Call("topsolid_create_parameter_expressions", expression)).Code == -32010, "Formula bypassed confirmation");
                foreach (var kind in new[] { "Real", "Integer", "Boolean", "Text" }) {
                    foreach (var mode in new[] { "formula", "reference", "literal" }) {
                        var entry = new JObject { ["name"] = "Derived", ["valueType"] = kind, ["mode"] = mode }; if (kind == "Real") entry["unitType"] = "Length";
                        if (mode == "formula") entry["formula"] = "Source"; else if (mode == "reference") entry["source"] = Handle(1); else { foreach (var prop in values[kind].Properties()) entry[prop.Name] = prop.Value.DeepClone(); }
                        var args = new JObject { ["documentId"] = "rev", ["parameters"] = new JArray(entry) }; Schema.Validate(args, Shape("topsolid_create_parameter_expressions")); ParameterExpressionTools.Validate(args, true);
                        var smart = ParameterExpressionTools.Build(entry); var dto = ParameterExpressionTools.Describe(smart);
                        try { ParameterExpressionTools.Verify(dto, entry); } catch (Exception ex) { throw new Exception(kind + "/" + mode + " synthetic definition: " + dto + " requested: " + entry, ex); }
                        Check((string)dto["mode"] == (mode == "formula" ? "Formula" : mode == "reference" ? "Element" : "Basic"), "Wrong native Smart type");
                        dto["mode"] = "None"; Throws<InvalidOperationException>(() => ParameterExpressionTools.Verify(dto, entry));
                        entry.Remove("name"); entry["element"] = Handle(2); Schema.Validate(args, Shape("topsolid_set_parameter_expressions")); ParameterExpressionTools.Validate(args, false);
                        Check(Throws<RpcException>(() => registry.Call("topsolid_set_parameter_expressions", args)).Code == -32010, "Expression edit bypassed confirmation");
                        entry["source"] = Handle(2); Throws<ArgumentException>(() => ParameterExpressionTools.Validate(args, false));
                        entry["source"]["documentId"] = "other"; Throws<ArgumentException>(() => ParameterExpressionTools.Validate(args, false));
                    }
                }
                var folderArgs = new JObject { ["documentId"] = "rev", ["folders"] = new JArray(new JObject { ["parentFolder"] = Handle(1), ["name"] = "Design parameters" }) };
                Check(Throws<RpcException>(() => registry.Call("topsolid_create_entity_folders", folderArgs)).Code == -32010, "Folder creation bypassed confirmation");
                folderArgs["folders"][0]["name"] = "$System"; Check(Throws<RpcException>(() => registry.Call("topsolid_create_entity_folders", folderArgs)).Code == -32602, "System folder name accepted");
                var move = new JObject { ["documentId"] = "rev", ["elements"] = new JArray(Handle(1)), ["destinationFolder"] = Handle(2) };
                Check(Throws<RpcException>(() => registry.Call("topsolid_move_entities", move)).Code == -32010, "Move bypassed confirmation");
                move["destinationFolder"]["documentId"] = "other"; Check(Throws<RpcException>(() => registry.Call("topsolid_move_entities", move)).Code == -32602, "Cross-document move accepted");
                var appearance = new JObject { ["documentId"] = "rev", ["changes"] = new JArray(new JObject { ["element"] = Handle(1), ["color"] = JObject.Parse("{r:0,g:128,b:255}"), ["transparency"] = .37, ["description"] = "" }) };
                Check(Throws<RpcException>(() => registry.Call("topsolid_update_elements", appearance)).Code == -32010, "Appearance bypassed confirmation");
                appearance["changes"][0]["color"]["r"] = 256; Check(Throws<RpcException>(() => registry.Call("topsolid_update_elements", appearance)).Code == -32602, "Invalid RGB byte accepted");
                var references = catalog.Where(t => (string)t["_meta"]["topsolid/category"] == "Parameters");
                Check(references.Count() >= 9 && references.All(t => ((JArray)t["_meta"]["topsolid/api"]).Count > 0), "Parameter category/source coverage missing");
            }
            nativeType = ParameterType.Real; storedValue = 0.012; var realRequest = (JObject)values["Real"].DeepClone(); realRequest["valueType"] = "Real";
            hasValue = false; pp.Calls.Clear(); var empty = access.Read(id); Check(!(bool)empty["hasValue"] && empty["value"].Type == JTokenType.Null && !pp.Calls.Contains("GetRealValue"), "Unset value read invented a number"); hasValue = true;
            nativeType = ParameterType.None; pp.Calls.Clear(); Check(!(bool)access.Read(id)["supported"] && !pp.Calls.Contains("HasValue"), "Nonparameter reached value interface"); nativeType = ParameterType.Real;
            linked = true; Throws<ArgumentException>(() => access.Preflight(id, realRequest)); linked = false;
            driven = true; Throws<ArgumentException>(() => access.Preflight(id, realRequest)); driven = false;
            editable = false; Throws<ArgumentException>(() => access.Preflight(id, realRequest)); editable = true;
            realRequest["unitType"] = "Angle"; Throws<ArgumentException>(() => access.Preflight(id, realRequest)); realRequest["unitType"] = "Length";
            nativeType = ParameterType.Text; specialText = true; var textRequest = new JObject { ["valueType"] = "Text", ["textValue"] = "x" }; Throws<ArgumentException>(() => access.Preflight(id, textRequest)); specialText = false;
            nativeType = ParameterType.Enumeration; pp.Calls.Clear(); var choices = access.Choices(id, new JObject { ["offset"] = 100, ["limit"] = 40 });
            Check((int)choices["total"] == 140 && (int)choices["returned"] == 40 && !(bool)choices["hasMore"] && (int)choices["items"][0]["value"] == 1007, "Enum keys were treated as positions or truncated");
            Check(pp.Calls.Count(c => c == "GetEnumerationValues") == 1, "Enum choices performed per-item native queries");
            var enumRequest = new JObject { ["valueType"] = "Enumeration", ["enumerationValue"] = 1397 }; access.Preflight(id, enumRequest); enumRequest["enumerationValue"] = 139; Throws<ArgumentException>(() => access.Preflight(id, enumRequest));
            foreach (var bad in new[] { "09/17/2026", "2026-02-30", "2026-09-17T12:00:00Z", "2026-09-17T12:00:00-07:00" }) Throws<ArgumentException>(() => ParameterValueInput.Date(new JValue(bad)));
            var tolerance = (JObject)values["Tolerance"].DeepClone(); tolerance["valueType"] = "Tolerance"; tolerance["lowerDeviationSI"] = .01; Throws<ArgumentException>(() => ParameterValueInput.Validate(tolerance, false));
            var preserved = new SmartReal(UnitType.Length, "2 * Diameter"); var old = new SmartReal(UnitType.Length, .02, "H7"); ParameterExpressionTools.Preserve(preserved, old); Check(preserved.Tolerance == "H7" && preserved.ScriptType == old.ScriptType, "Expression edit lost tolerance/dialect");
            EntityStructureTools.RequireAcyclic(id, parent, _ => ElementId.Empty); checks++;
            Throws<ArgumentException>(() => EntityStructureTools.RequireAcyclic(id, id, _ => ElementId.Empty));
            Throws<ArgumentException>(() => EntityStructureTools.RequireAcyclic(id, parent, _ => id));
            Throws<ArgumentException>(() => EntityStructureTools.RequireAcyclic(id, parent, _ => parent));
            var rebased = MutationReferences.Rebase(JObject.Parse("{documentId:'rev',parameters:[{element:{documentId:'rev',id:1},source:{documentId:'rev',id:2}}]}"), "dirty");
            Check((string)rebased["parameters"][0]["source"]["documentId"] == "dirty", "Associative source handle did not rebase with target");
        }
    }
}
