using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void PdmDateSorting()
        {
            var rows = new[] {
                JObject.Parse("{pdmObjectId:'a',name:'Same',creationDate:'2026-09-16',creationDateStatus:'available'}"),
                JObject.Parse("{pdmObjectId:'b',name:'Same',creationDate:'2009-11-05',creationDateStatus:'available'}") };
            var args = JObject.Parse("{orderBy:'oldestFirst',limit:1}");
            var first = PdmCreationDates.Sort(rows, args);
            Check((bool)first["sortApplied"] && (string)first["items"][0]["pdmObjectId"] == "b", "Sort globally before taking the page; preserve duplicate names");
            args["offset"] = 1;
            Check((string)PdmCreationDates.Sort(rows, args)["items"][0]["pdmObjectId"] == "a", "Sorted page continuation");
            args["offset"] = 0; args["orderBy"] = "newestFirst";
            Check((string)PdmCreationDates.Sort(rows, args)["items"][0]["pdmObjectId"] == "a", "Newest first");
            rows[0]["creationDate"] = "09/11/2026";
            var failure = PdmCreationDates.Sort(rows, args);
            Check(!(bool)failure["sortApplied"] && failure["items"] == null, "Ambiguous date must not yield a claimed chronological list");
            var unavailable = PdmCreationDates.Unsortable(50, "creationDateUnavailableOrAmbiguous");
            Check(unavailable["items"] == null && (int)unavailable["total"] == 50, "Missing dates retain count without unsorted fallback");
            var dateRow = new PdmCreationDates((id, name) => "2021-04-09T15:39:20.7929459").Get("pdm", "name");
            Check((string)dateRow["creationDateSource"] == "backingDocument.creationDateParameter" && (string)dateRow["creationDateStatus"] == "available", "Creation date source must remain explicit");
            Check((string)new PdmCreationDates((id,name) => null).Get("pdm", "name")["creationDateStatus"] == "unavailable", "Missing creation parameter cannot become a made-up date");
            var names = new[] {
                JObject.Parse("{pdmObjectId:'z',name:'Zulu'}"), JObject.Parse("{pdmObjectId:'b',name:'Alpha'}"),
                JObject.Parse("{pdmObjectId:'a',name:'Alpha'}"), JObject.Parse("{pdmObjectId:'k',name:'가공'}") };
            var nameArgs = JObject.Parse("{orderBy:'nameAscending',offset:0,limit:1}");
            var namePage = PdmCreationDates.Sort(names, nameArgs);
            Check((bool)namePage["sortApplied"] && (int)namePage["total"] == 4 && (string)namePage["items"][0]["pdmObjectId"] == "a", "Names sort globally without creation dates and retain duplicates");
            nameArgs["offset"] = 1;
            Check((string)PdmCreationDates.Sort(names, nameArgs)["items"][0]["pdmObjectId"] == "b", "Alphabetical pagination must preserve distinct equal names");
            nameArgs["offset"] = 0; nameArgs["orderBy"] = "nameDescending";
            Check((string)PdmCreationDates.Sort(names, nameArgs)["items"][0]["pdmObjectId"] == "k", "Reverse alphabetical order includes Korean names");
            var context = PdmCreationContextTools.Describe("alpha", names, ".TopAsm", true);
            Check(((JArray)context["projectMatches"]).Count == 2 && !(bool)context["canChooseUniqueProject"], "Creation context must preserve ambiguous case-insensitive matches");
            Check((string)context["creation"]["extension"] == ".TopAsm" && !(bool)context["creation"]["loadedDocumentRequired"], "Creation context must support an extension without loaded examples");
            context = PdmCreationContextTools.Describe("Zulu", names, ".TopPrt", false);
            Check(!(bool)context["canChooseUniqueProject"], "Partial lookup cannot establish a unique destination");
        }
    }
}
