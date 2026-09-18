using System;
using System.ServiceModel;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CamNames
    {
        internal static string Name(ElementId id)
        {
            if (id.IsEmpty) return null;
            var friendly = TopSolidHost.Elements.GetFriendlyName(id);
            return string.IsNullOrWhiteSpace(friendly) ? TopSolidHost.Elements.GetName(id) : friendly;
        }
        internal static string Name(ElementExId id) => id.IsEmpty ? null : id.IsCreated ? Name(id.ElementId) : TopSolidCamHost.Operations.IsOperation(id) ? TopSolidCamHost.Operations.GetDescription(id) : null;
        internal static string OperationName(ElementExId id)
        {
            if (id.IsEmpty) return null;
            // Kernel friendly names can be the same generic translated type caption for
            // every CAM operation. CAM descriptions include the specific task/strategy.
            var description = TopSolidCamHost.Operations.GetDescription(id);
            return string.IsNullOrWhiteSpace(description) ? Name(id) : description;
        }
        internal static JToken OperationNamed(ElementId id) => Enrich(AutomationValues.Json(id), () => OperationName(new ElementExId(id)));
        internal static JToken OperationNamed(ElementExId id) => Enrich(AutomationValues.Json(id), () => OperationName(id));
        internal static JToken Named(ElementId id) => Enrich(AutomationValues.Json(id), () => Name(id));
        internal static JToken Named(ElementExId id) => Enrich(AutomationValues.Json(id), () => Name(id));
        private static JToken Enrich(JToken value, Func<string> read)
        {
            if (!(value is JObject row)) return value;
            try { row["name"] = read(); row["friendlyName"] = row["name"].DeepClone(); }
            catch (TimeoutException) { throw; }
            catch (CommunicationException ex) when (!(ex is FaultException)) { throw; }
            catch (Exception ex) { row["nameUnavailable"] = true; row["nameError"] = ex.GetType().Name + ": " + ex.Message; }
            return row;
        }
        internal static JObject Operation(ElementExId id)
        {
            var tool = TopSolidCamHost.Operations.GetTool(id); var part = TopSolidCamHost.Operations.GetPart(id);
            var operationInfo = OperationNamed(id); var toolInfo = Named(tool); var partInfo = Named(part);
            return new JObject { ["operation"] = AutomationValues.Json(id), ["operationName"] = operationInfo["name"]?.DeepClone(),
                ["description"] = TopSolidCamHost.Operations.GetDescription(id), ["upToDate"] = TopSolidCamHost.Operations.IsUpToDate(id),
                ["tool"] = AutomationValues.Json(tool), ["toolName"] = toolInfo is JObject ? toolInfo["name"]?.DeepClone() : null,
                ["part"] = AutomationValues.Json(part), ["partName"] = partInfo is JObject ? partInfo["name"]?.DeepClone() : null,
                ["operationInfo"] = operationInfo, ["toolInfo"] = toolInfo, ["partInfo"] = partInfo };
        }
    }
}
