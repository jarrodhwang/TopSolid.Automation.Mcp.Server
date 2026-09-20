using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CamMethodTools
    {
        internal const string ExecuteName = "topsolid_execute_cam_method";
        internal static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_cam_stages", "Read exact native modeling and machining stage identities without switching stages.",
                new JObject { ["documentId"] = Schema.Text("Exact CAM document revision.") }, p => a.Read("cam", () => CamStages.Read(a.Document(p))),
                "Cam/Operation", new[] { "documentId" }, api: ApiRefs.Kernel("IOperations.GetStages", "IOperations.GetModelingStage", "IOperations.GetWorkingStage")));
            register(new ToolDefinition("topsolid_inspect_cam_method", "Verify an existing loaded CAM method by exact PDM identity and return its current revision. Does not open or execute it.",
                new JObject { ["pdmObjectId"] = Schema.Text("Method PDM identity.") }, p => a.Read("cam", () => Inspect((string)p["pdmObjectId"])),
                "Cam/Operation", new[] { "pdmObjectId" }, api: ApiRefs.Kernel("IDocuments.GetDocument", "IDocuments.GetTypeFullName")));
            var schema = DocumentActionTools.Target();
            schema["workpiece"] = Schema.Element(); schema["machiningStage"] = Schema.Element();
            schema["method"] = MethodSchema();
            schema["colors"] = Schema.Object(CamColorTools.PlanProperties(), "documentId", "workpiece", "palette", "targets");
            schema["executionId"] = Schema.Text("Unique reviewed execution attempt ID (UUID). Never reuse to perform another execution.", 36);
            register(new ToolDefinition(ExecuteName, "Execute one registered CAM method in an exact machining stage after final confirmation. Requires current geometry and exact RGB, checks color collisions, typed inputs and method revision. Returns created operation states; deferred is not calculated. No save or NC.",
                schema, p => Execute(a, p), "Cam/Operation", new[] { "documentId", "workpiece", "machiningStage", "method", "colors", "executionId" }, false,
                ApiRefs.Cam("IOperations.ExecuteMethod", "IOperations.IsUpToDate", "MethodExecutionOptions").Concat(ApiRefs.Kernel("IOperations.SetWorkingStage", "IOperations.ResetInsertionOperation")).ToArray(),
                Validate, p => Preview(a, p)));
        }
        private static JObject MethodSchema()
        {
            var options = new JObject();
            foreach (var property in typeof(CamMethodOptions).GetProperties()) options[property.Name] = Schema.Boolean("Reviewed native execution option.");
            var scalar = new JObject { ["anyOf"] = new JArray(Schema.TextValue("Registered or user-provided text",1024),
                Schema.Number("Registered or user-provided SI real",-1e12,1e12), Schema.Boolean("Boolean input"), new JObject { ["type"]="null" }) };
            var input = Schema.Object(new JObject { ["Name"]=Schema.Text("Existing target-document parameter name",256),["Label"]=Schema.TextValue("Input label",120),
                ["ValueType"]=Schema.Choice("Exact input type","text","real","integer","boolean"),["UnitType"]=Schema.TextValue("Native real unit type, otherwise empty",80),["Value"]=scalar },
                "Name","Label","ValueType","UnitType","Value");
            var properties = new JObject { ["Id"]=Schema.Text("Registered method key",64),["PdmObjectId"]=Schema.Text("Exact method PDM identity",256),
                ["MethodDocumentId"]=Schema.Text("Verified method revision",256),["Name"]=Schema.Text("Registered display name",120),["Process"]=Schema.Text("Machining process",80),
                ["Conditions"]=Schema.TextValue("Registered applicability conditions",4000),["SearchScope"]=Schema.Choice("Method lookup scope","document","workpiece"),
                ["WorkpieceParameter"]=Schema.TextValue("Reserved; must be empty on this host",256),["AfterMethods"]=Schema.Array(Schema.Text("Prerequisite method key",64),0,32),
                ["Colors"]=CamColorTools.PlanProperties()["palette"].DeepClone(),["Options"]=Schema.Object(options,options.Properties().Select(p=>p.Name).ToArray()),
                ["Inputs"]=Schema.Array(input,0,32) };
            return Schema.Object(properties,properties.Properties().Select(p=>p.Name).ToArray());
        }
        internal static JObject Inspect(string pdm)
        {
            var objectId = new PdmObjectId(pdm); var doc = TopSolidHost.Documents.GetDocument(objectId);
            if (doc.IsEmpty || !TopSolidHost.Documents.GetDocuments().Contains(doc)) throw new ArgumentException("Open the selected CAM method in TopSolid before registering or executing it.");
            var type = TopSolidHost.Documents.GetTypeFullName(doc);
            if (!type.StartsWith("TopSolid.Cam.NC.", StringComparison.Ordinal) || type.IndexOf("Method", StringComparison.Ordinal) < 0)
                throw new ArgumentException("The selected document is not a native CAM method.");
            return new JObject { ["pdmObjectId"] = pdm, ["methodDocumentId"] = doc.PdmDocumentId, ["name"] = TopSolidHost.Documents.GetName(doc),
                ["type"] = type, ["isDirty"] = TopSolidHost.Documents.IsDirty(doc) };
        }
        internal static void Validate(JObject p)
        {
            MutationReferences.Validate(p);
            if (!Guid.TryParse((string)p["executionId"], out _)) throw new ArgumentException("Invalid execution ID.");
            if (p["method"] is not JObject definition || definition.Properties().Any(property => !typeof(CamMethodDefinition).GetProperties().Any(info => info.Name == property.Name)) ||
                definition["Options"] is not JObject options || options.Count != 6 || options.Properties().Any(property => property.Value.Type != JTokenType.Boolean || !typeof(CamMethodOptions).GetProperties().Any(info => info.Name == property.Name)))
                throw new ArgumentException("Use the exact registered method snapshot and six Boolean execution options.");
            var method = p["method"]?.ToObject<CamMethodDefinition>() ?? throw new ArgumentException("Missing method."); method.Validate();
            if (p["colors"] is not JObject colors || (string)colors["documentId"] != (string)p["documentId"] ||
                !JToken.DeepEquals(colors["workpiece"], p["workpiece"]) || !JToken.DeepEquals(colors["palette"], JObject.FromObject(method.Colors))) throw new ArgumentException("Color contract mismatch.");
            CamColorTools.Validate(colors);
        }
        private static JObject Preview(AutomationGateway a, JObject p)
        {
            Validate(p); var result = a.PreviewDocument(p, "cam");
            var doc = a.Document(p); if (!CamStages.IsCam(doc)) throw new ArgumentException("Select a CAM workpiece document.");
            result["requiredStage"] = AutomationValues.Json(CamStages.Resolve(a, p, EditStage.Machining));
            var method = p["method"].ToObject<CamMethodDefinition>(); var actual = Inspect(method.PdmObjectId);
            if ((string)actual["methodDocumentId"] != method.MethodDocumentId || (bool?)actual["isDirty"] == true)
                throw new InvalidOperationException("The registered method revision changed or has unsaved edits. Re-register the reviewed revision.");
            result["method"] = actual; result["options"] = JObject.FromObject(method.Options);
            var colors = (JObject)p["colors"]; var geometry = new CamColorGeometry(a, colors);
            var rows = CamColorTools.Verify(colors, geometry.Read);
            var roles = method.Colors.Roles.ToDictionary(r => r.Key);
            for (int i = 0; i < rows.Count; i++) if (!JToken.DeepEquals(rows[i]["color"], roles[(string)colors["targets"][i]["roleKey"]].Rgb))
                throw new InvalidOperationException("Apply the reviewed preparation colors before executing this method.");
            CheckColorScope(a, colors, method);
            result["colors"] = new JArray(rows.Select(row => new JObject { ["target"] = row["target"], ["fingerprint"] = row["fingerprint"], ["color"] = row["color"] }));
            result["inputs"] = new JArray(method.Inputs.Select(input => {
                var value = Input(input); var id = TopSolidHost.Elements.SearchByName(doc, input.Name);
                if (id.IsEmpty) throw new ArgumentException("Missing target-document method parameter: " + input.Name);
                ParameterValues.Native.Preflight(id, value);
                return new JObject { ["name"] = input.Name, ["before"] = ParameterValues.Native.Read(id), ["after"] = value };
            }));
            return result;
        }
        private static void CheckColorScope(AutomationGateway a, JObject colors, CamMethodDefinition method)
        {
            var all = new CamColorGeometry(a, new JObject { ["documentId"] = colors["documentId"] }, true);
            if (method.SearchScope == "workpiece" && (all.Workpieces.Count != 1 || !JToken.DeepEquals(all.Workpieces[0]["element"], colors["workpiece"])))
                throw new InvalidOperationException("This host cannot bind ExecuteMethod to one of several workpieces. Use an isolated workpiece document or a reviewed document-scope method.");
            foreach (var target in all.Targets)
            {
                var color = target["face"] is JObject face
                    ? ElementAppearance.Json(TopSolidHost.Shapes.GetFaceColor(a.Item(new JObject { ["item"] = face.DeepClone() })))
                    : ElementAppearance.Json(TopSolidHost.Elements.GetColor(a.Element(target)));
                if (target["face"] is JObject inherited && (bool?)color?["empty"] == true)
                    color = ElementAppearance.Json(TopSolidHost.Elements.GetColor(a.Element(new JObject { ["element"] = inherited["element"].DeepClone() })));
                VerifyScopeTarget((JArray)colors["targets"], method.Colors, target, color);
            }
        }
        internal static void VerifyScopeTarget(JArray assigned, CamColorStandard palette, JObject target, JToken effectiveColor)
        {
            var role = palette.Roles.FirstOrDefault(r => JToken.DeepEquals(r.Rgb, effectiveColor));
            if (role == null) return;
            bool allowed = assigned.OfType<JObject>().Any(entry => (string)entry["roleKey"] == role.Key && (JToken.DeepEquals(entry["target"], target) ||
                target["face"] != null && JToken.DeepEquals(entry["target"]?["element"], target["face"]?["element"])));
            if (!allowed) throw new InvalidOperationException("Method RGB also matches unapproved geometry in its document-wide search scope. Review or isolate that geometry first.");
        }
        internal static JObject Input(CamMethodInput input)
        {
            input.Validate(); if (input.Value == null || input.Value.Type == JTokenType.Null) throw new ArgumentException("Enter method input: " + input.Label);
            var type = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ValueType);
            var value = new JObject { ["valueType"] = type };
            value[input.ValueType == "real" ? "realValueSI" : input.ValueType + "Value"] = input.Value.DeepClone();
            if (input.ValueType == "real") value["unitType"] = input.UnitType;
            ParameterValueInput.Validate(value, false); return value;
        }
        private static JObject Execute(AutomationGateway a, JObject p)
        {
            Preview(a, p);
            // Reserve durably before entering native execution. A lost transport must
            // never re-run a method whose native outcome is unknown.
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TopSolid.Automation.AI.Studio", "cam-executions");
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, Guid.Parse((string)p["executionId"]).ToString("N") + ".json");
            using (var output = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(output)) writer.Write(new JObject { ["status"] = "started", ["arguments"] = p.DeepClone() }.ToString(Formatting.None));
            try
            {
                var result = a.Modify(p, "execute CAM method", "cam", (doc, current) => {
                    Preview(a, current);
                    var method = current["method"].ToObject<CamMethodDefinition>();
                    foreach (var input in method.Inputs)
                    {
                        var id = TopSolidHost.Elements.SearchByName(doc, input.Name); var value = Input(input);
                        ParameterValues.Native.Set(id, value); ParameterValues.Verify(ParameterValues.Native.Read(id), value);
                    }
                    var options = method.Options;
                    var native = new MethodExecutionOptions(options.LaunchDeferred, options.KeepAssociativity, options.ManualExecution, options.UseCuttingConditions, options.ReuseAnswers) { SilentMode = options.SilentMode };
                    var operations = TopSolidCamHost.Operations.ExecuteMethod(doc, new DocumentId(method.MethodDocumentId), native);
                    if (operations == null || operations.Count == 0) throw new InvalidOperationException("The method created no operations or was cancelled.");
                    if (operations.Count > 256) throw new InvalidOperationException("Method exceeded the 256-operation receipt limit.");
                    var rows = new JArray();
                    foreach (var id in operations)
                    {
                        AutomationGateway.RequireValid(id, "method operation");
                        if (!id.DocumentId.Equals(doc) || !CamStages.Contains(a.Element(current, "machiningStage"), id, 0))
                            throw new InvalidOperationException("A generated operation is outside the approved document or machining stage.");
                        var extended = new ElementExId(id);
                        var isCam = TopSolidCamHost.Operations.IsOperation(extended);
                        var updated = isCam && TopSolidCamHost.Operations.IsUpToDate(extended);
                        rows.Add(new JObject { ["element"] = AutomationValues.Json(id), ["name"] = TopSolidHost.Elements.GetFriendlyName(id), ["isCamOperation"] = isCam, ["upToDate"] = updated });
                    }
                    return new JObject { ["executionId"] = current["executionId"], ["methodDocumentId"] = method.MethodDocumentId,
                        ["status"] = options.LaunchDeferred ? "deferred" : rows.All(r => (bool?)r["upToDate"] == true) ? "calculated" : "pending",
                        ["operations"] = rows, ["ncGenerated"] = false };
                }, EditStage.Machining);
                File.WriteAllText(file, result.ToString(Formatting.None)); return result;
            }
            catch (Exception ex)
            {
                File.WriteAllText(file, new JObject { ["status"] = "failedOrUnknown", ["executionId"] = p["executionId"], ["detail"] = ex.Message }.ToString(Formatting.None));
                throw;
            }
        }
    }
}
