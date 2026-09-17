using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed class PdmCreationDates
    {
        private readonly Func<string, string, string> read;
        public const string UnavailableReason = "No verified creation-date parameter is available on this object's backing document. Do not substitute a modification date or infer a date from the name.";
        internal PdmCreationDates(Func<string, string, string> read = null) { this.read = read; }
        public bool IsAvailable => read != null;
        internal static JObject Unsortable(int total, string reason) => new JObject {
            ["sortApplied"] = false, ["total"] = total, ["reason"] = reason,
            ["message"] = "Cannot sort by creation date: at least one object has a missing or ambiguous creation date. No chronological list was produced." };
        internal static JObject Sort(JObject[] rows, JObject p)
        {
            if ((string)p["orderBy"] == "nameAscending" || (string)p["orderBy"] == "nameDescending")
            {
                var byName = (string)p["orderBy"] == "nameDescending"
                    ? rows.OrderByDescending(r => (string)r["name"], StringComparer.InvariantCultureIgnoreCase)
                    : rows.OrderBy(r => (string)r["name"], StringComparer.InvariantCultureIgnoreCase);
                var page = AutomationValues.Page(byName.ThenBy(r => (string)r["pdmObjectId"], StringComparer.Ordinal), p, r => r, 100);
                page["sortApplied"] = true; page["orderBy"] = p["orderBy"].DeepClone();
                page["nameComparison"] = "InvariantCultureIgnoreCase; duplicate names retained; PDM ID breaks ties";
                return page;
            }
            var formats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", "yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-dd HH:mm:ss" };
            var parsed = new System.Collections.Generic.List<Tuple<JObject, DateTime>>();
            foreach (var row in rows)
            {
                if ((string)row["creationDateStatus"] != "available" || !DateTime.TryParseExact((string)row["creationDate"], formats,
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)) return Unsortable(rows.Length, "creationDateUnavailableOrAmbiguous");
                parsed.Add(Tuple.Create(row, date));
            }
            var ordered = (string)p["orderBy"] == "newestFirst" ? parsed.OrderByDescending(r => r.Item2) : parsed.OrderBy(r => r.Item2);
            var result = AutomationValues.Page(ordered.Select(r => r.Item1), p, r => r, 100);
            result["sortApplied"] = true; result["orderBy"] = p["orderBy"].DeepClone(); return result;
        }
        public JObject Get(string id, string expectedName)
        {
            var row = new JObject { ["creationDate"] = JValue.CreateNull(), ["creationDateStatus"] = "unavailable" };
            if (read == null) { row["creationDateNote"] = UnavailableReason; return row; }
            try
            {
                var date = read(id, expectedName);
                if (!string.IsNullOrWhiteSpace(date)) { row["creationDate"] = date; row["creationDateStatus"] = "available"; row["creationDateSource"] = "backingDocument.creationDateParameter"; }
                else row["creationDateNote"] = UnavailableReason;
            }
            catch (System.ServiceModel.CommunicationException ex) when (!(ex is System.ServiceModel.FaultException)) { throw; }
            catch (TimeoutException) { throw; }
            catch (Exception) { row["creationDateNote"] = UnavailableReason; }
            return row;
        }
        public static string[] Api => Tools.ApiRefs.Kernel("IDocuments.GetDocument", "IDocuments.Exists", "IDocuments.GetPdmObject", "IParameters.GetCreationDateParameter", "IParameters.GetDateTimeValue", "IParameters.GetParameterType", "IElements.Exists");
    }
    internal sealed partial class AutomationGateway
    {
        public PdmCreationDates CreationDates() => new PdmCreationDates((id, expectedName) => {
            var objectId = new PdmObjectId(id);
            var doc = TopSolidHost.Documents.GetDocument(objectId);
            if (doc.IsEmpty || !TopSolidHost.Documents.Exists(doc) || !TopSolidHost.Documents.GetPdmObject(doc).Equals(objectId)) return null;
            var parameter = TopSolidHost.Parameters.GetCreationDateParameter(doc);
            if (parameter.IsEmpty || !TopSolidHost.Elements.Exists(parameter) || TopSolidHost.Parameters.GetParameterType(parameter) != ParameterType.DateTime) return null;
            var date = TopSolidHost.Parameters.GetDateTimeValue(parameter);
            return date == DateTime.MinValue ? null : date.ToString("o", CultureInfo.InvariantCulture);
        });
        public JObject ListPdmProjects(bool working, JObject p)
        {
            var ids = TopSolidHost.Pdm.GetProjects(working, !working);
            var sorting = p["orderBy"] != null;
            var chronological = (string)p["orderBy"] == "oldestFirst" || (string)p["orderBy"] == "newestFirst";
            var dates = chronological || (bool?)p["includeCreationDates"] == true ? CreationDates() : null;
            if (chronological && !dates.IsAvailable) return PdmCreationDates.Unsortable(ids.Count, "creationDateServiceUnavailable");
            Func<PdmObjectId, JObject> project = id =>
            {
                var name = TopSolidHost.Pdm.GetName(id);
                var row = new JObject { ["pdmObjectId"] = id.Id, ["name"] = name };
                if (dates != null) row.Merge(dates.Get(id.Id, name)); return row;
            };
            if (sorting) return PdmCreationDates.Sort(ids.Select(project).ToArray(), p);
            return AutomationValues.Page(ids, p, id => project(id), 100);
        }
    }
}
