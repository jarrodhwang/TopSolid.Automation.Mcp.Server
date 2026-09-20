using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal enum EditStage { None, Modeling, Machining, Target }
    internal static class CamStages
    {
        internal static bool IsCam(DocumentId document) => TopSolidHost.Documents.GetTypeFullName(document).StartsWith("TopSolid.Cam.NC.", StringComparison.Ordinal);
        internal static bool IsMachining(string type) => type == "TopSolid.Cam.NC.Kernel.DB.Stages.MachiningStageOperation" ||
            type == "TopSolid.Cam.NC.Kernel.DB.Stages.RemachiningStageOperation";
        internal static JObject Read(DocumentId document)
        {
            var api = TopSolidHost.Operations;
            return new JObject { ["documentId"] = document.PdmDocumentId, ["isCam"] = IsCam(document),
                ["workingStage"] = AutomationValues.Json(api.GetWorkingStage(document)),
                ["modelingStage"] = AutomationValues.Json(api.GetModelingStage(document)),
                ["stages"] = new JArray(api.GetStages(document).Select(id => new JObject {
                    ["element"] = AutomationValues.Json(id), ["name"] = TopSolidHost.Elements.GetFriendlyName(id),
                    ["type"] = TopSolidHost.Elements.GetTypeFullName(id), ["machining"] = IsMachining(TopSolidHost.Elements.GetTypeFullName(id)) })) };
        }
        internal static ElementId Resolve(AutomationGateway gateway, JObject arguments, EditStage intent)
        {
            var document = gateway.Document(arguments);
            if (!IsCam(document) || intent == EditStage.None) return default(ElementId);
            var api = TopSolidHost.Operations;
            if (intent == EditStage.Modeling)
            {
                var stage = api.GetModelingStage(document);
                if (stage.IsEmpty) throw new InvalidOperationException("This CAM document has no modeling stage.");
                return stage;
            }
            var stages = api.GetStages(document);
            var candidates = stages.Where(id => IsMachining(TopSolidHost.Elements.GetTypeFullName(id))).ToArray();
            if (intent == EditStage.Target)
            {
                var handles = arguments["element"] is JObject element ? new[] { element } :
                    arguments["elements"] is JArray elements ? elements.OfType<JObject>().ToArray() :
                    (arguments["changes"] as JArray ?? new JArray()).OfType<JObject>().Select(c => (JObject)c["element"]).ToArray();
                var required = handles.Select(handle => {
                    var target = gateway.Element(new JObject { ["element"] = handle.DeepClone() });
                    if (stages.Contains(target)) throw new ArgumentException("Stage edits require a dedicated native workflow.");
                    var owners = candidates.Where(s => Contains(s, target, 0)).ToArray();
                    if (owners.Length > 1) throw new ArgumentException("The element belongs to multiple machining stages.");
                    return owners.Length == 1 ? owners[0] : api.GetModelingStage(document);
                }).Distinct().ToArray();
                if (required.Length != 1 || required[0].IsEmpty) throw new ArgumentException("Review separate batches for elements in different CAM stages.");
                return required[0];
            }
            if (arguments["machiningStage"] is JObject)
            {
                var stage = gateway.Element(arguments, "machiningStage");
                if (!candidates.Contains(stage)) throw new ArgumentException("Choose a machining stage in the exact document.");
                return stage;
            }
            if (arguments["element"] is JObject)
            {
                var target = gateway.Element(arguments);
                // Use native stage children, never the localized stage title.
                var owners = candidates.Where(s => Contains(s, target, 0)).ToArray();
                if (owners.Length == 1) return owners[0];
            }
            if (candidates.Length == 1) return candidates[0];
            throw new ArgumentException("Select one exact machining stage; multiple stages are available.");
        }
        internal static bool Contains(ElementId parent, ElementId target, int depth)
        {
            if (depth > 16) return false;
            return TopSolidHost.Operations.GetChildren(parent).Any(c => c.Equals(target) || TopSolidHost.Operations.IsOperation(c) && Contains(c, target, depth + 1));
        }
        internal static void Enter(AutomationGateway gateway, JObject arguments, EditStage intent)
        {
            var stage = Resolve(gateway, arguments, intent);
            if (stage.IsEmpty) return;
            TopSolidHost.Operations.SetWorkingStage(stage);
            TopSolidHost.Operations.ResetInsertionOperation(gateway.Document(arguments));
            if (!TopSolidHost.Operations.GetWorkingStage(gateway.Document(arguments)).Equals(stage))
                throw new InvalidOperationException("TopSolid did not enter the requested working stage.");
        }
    }
}
