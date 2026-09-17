using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void DocumentCreation()
        {
            var input = JObject.Parse("{ownerId:'exact-project',name:'Empty part'}");
            var part = DocumentCreationCatalog.PartArguments(input);
            Check((string)part["extension"] == ".TopPrt" && !DocumentCreationCatalog.UseDefaultTemplate(part), "Plain parts must resolve with no native document query or template");
            Check(input["extension"] == null && input["useDefaultTemplate"] == null, "Do not modify the client's approved arguments");
            foreach (var extension in new[] { ".TopPrt", ".TopAsm", ".Top2D", ".Top3D", ".TopDft", ".TopMillTurn", ".TopPdf", ".TopNCOpConfig" })
            {
                var args = new JObject { ["extension"] = extension };
                PdmActionTools.ValidateDocument(args);
                Check(!DocumentCreationCatalog.UseDefaultTemplate(args), "Omitted template flag must create empty " + extension);
                args["useDefaultTemplate"] = true;
                Check(DocumentCreationCatalog.UseDefaultTemplate(args), "Explicit default-template opt-in must remain supported");
            }
            Check(DocumentCreationCatalog.Extension(".topprt") == ".TopPrt", "Known extension case normalization");
            Check(DocumentCreationCatalog.Extension(".TopVendorExtension") == ".TopVendorExtension", "Preserve explicit installation-specific native extensions; host remains authoritative");
            foreach (var bad in new[] { "TopPrt", ".TopPrt;.TopAsm", "c:\\x.TopPrt", ".TopPrt ", ".TopPrt\n", ".Top", ".TopPrj", ".pdf", ".png", ".txt", ".xml", ".bin", ".topfud" })
                Throws<ArgumentException>(() => PdmActionTools.ValidateDocument(new JObject { ["extension"] = bad }));
            foreach (var bad in new[] { "{}", "{templateId:'template',extension:'.TopPrt'}", "{templateId:'template',useDefaultTemplate:false}" })
                Throws<ArgumentException>(() => PdmActionTools.ValidateDocument(JObject.Parse(bad)));
            PdmActionTools.ValidateDocument(JObject.Parse("{templateId:'explicit-template'}"));
            DocumentCreationCatalog.RequireIdle(null); DocumentCreationCatalog.RequireIdle("");
            Check(Throws<InvalidOperationException>(() => DocumentCreationCatalog.RequireIdle("Sketch edit")).Message.Contains("No creation was attempted"), "Busy command should fail before PDM creation with useful recovery");
            using (var gateway = new AutomationGateway())
            {
                var registry = new ToolRegistry(gateway);
                var page = registry.Call("topsolid_list_document_types", new JObject { ["limit"] = 100 });
                Check(!(bool)page["isError"], "Catalog works without any TopSolid connection");
                var data = JObject.Parse((string)page["content"][0]["text"]);
                var next = (bool)data["hasMore"] ? JObject.Parse((string)registry.Call("topsolid_list_document_types", new JObject { ["offset"] = ((JArray)data["items"]).Count, ["limit"] = 100 })["content"][0]["text"]) : null;
                var count = ((JArray)data["items"]).Count + (next == null ? 0 : ((JArray)next["items"]).Count);
                Check(count == DocumentCreationCatalog.Entries().Count, "Catalog pagination must not omit any supplied extension");
                var native = DocumentCreationCatalog.Entries().OfType<JObject>().Where(r => (string)r["route"] == "topsolid_create_document").ToArray();
                Check(native.Length == native.Select(r => (string)r["extension"]).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Catalog cannot duplicate native extension identities");
                foreach (var row in native) Check(DocumentCreationCatalog.Extension((string)row["extension"]) == (string)row["extension"], "Every catalog native extension is accepted");
            }
        }
    }
}
