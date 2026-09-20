using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class CamMethodBrowserTests
{
    internal static async Task Run()
    {
        foreach (var source in new[] { "topsolid_list_projects", "topsolid_list_libraries" }) {
            var client = new Client(); var roots = new CamMethodBrowser.Pages(client, source);
            await roots.Fetch(CancellationToken.None);
            Check.Equal(source == "topsolid_list_projects" ? "project" : "library", (string)roots.Rows[0]["pdmObjectId"]!, "Wrong PDM root");
            var page = new CamMethodBrowser.Pages(client, "topsolid_list_pdm_children", (string)roots.Rows[0]["pdmObjectId"]!);
            await page.Fetch(CancellationToken.None);
            Check.True(page.More && !page.Rows.Any(CamMethodBrowser.IsMethod), "Non-method page skipped pagination");
            await page.Fetch(CancellationToken.None);
            Check.True(!page.More && page.Rows.Any(CamMethodBrowser.IsMethod), "Method missing on second page");
            var selected = await CamMethodBrowser.Select(client, page.Rows.Single(CamMethodBrowser.IsMethod), (_, _) => Task.FromResult(true), CancellationToken.None);
            Check.Equal("method-rev", (string)selected!["methodDocumentId"]!, "Revision receipt");
            Check.Equal(0, client.Writes, "Browsing a loaded method mutated TopSolid");
        }
        foreach (var fault in new[] { "none", "decline", "proposal", "dirty", "revision", "identity", "cancel" }) {
            var client = new Client { Fault = fault }; var row = Client.Method(); row["isLoaded"] = false;
            using var cancel = new CancellationTokenSource();
            var rejected = false;
            try {
                var result = await CamMethodBrowser.Select(client, row, (proposal, _) => {
                    Check.True(proposal["confirmationToken"] == null, "Approval exposed token");
                    if (fault == "cancel") cancel.Cancel();
                    return Task.FromResult(fault != "decline");
                }, cancel.Token);
                Check.True(fault == "decline" ? result == null : fault == "none" && result != null, "Invalid selection accepted: " + fault);
            } catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException) { rejected = true; }
            Check.Equal(fault is not ("none" or "decline"), rejected, "Method rejection: " + fault);
            Check.Equal(fault is "decline" or "proposal" or "cancel" ? 0 : 1, client.Writes, "Open authorization: " + fault);
        }
        var malformed = new Client { Fault = "page" }; var invalid = new CamMethodBrowser.Pages(malformed, "topsolid_list_projects");
        try { await invalid.Fetch(CancellationToken.None); throw new Exception("Malformed page accepted"); } catch (InvalidOperationException) { }
        Check.Equal(0, invalid.Rows.Count, "Malformed page partially cached");
        malformed.Fault = "none"; await invalid.Fetch(CancellationToken.None); Check.Equal(1, invalid.Rows.Count, "Retry after invalid page");
        var fake = Client.Method(); fake["extension"] = ".TopPrt"; fake["name"] = "CAM Method";
        Check.True(!CamMethodBrowser.IsMethod(fake), "Filename inferred method type");
    }
    internal sealed class Client : IConfirmableMcpClient
    {
        internal string Fault = "none";
        internal int Writes;
        public bool IsConnected => true;
        public IReadOnlyList<McpToolDefinition> Tools => [];
        internal static JObject Method() => JObject.Parse("{pdmObjectId:'method-pdm',documentId:'method-rev',name:'Facing method',kind:'document',extension:'.TopMillTurnMethod',isLoaded:true}");
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            JObject data;
            if (name == "topsolid_inspect_cam_method") data = new JObject { ["name"] = "Facing method", ["pdmObjectId"] = Fault == "identity" ? "other" : "method-pdm", ["methodDocumentId"] = Fault == "revision" ? "stale" : "method-rev", ["isDirty"] = Fault == "dirty" };
            else {
                var offset = (int)args["offset"]!; JArray rows; bool more = false;
                if (name is "topsolid_list_projects" or "topsolid_list_libraries") rows = new JArray(new JObject { ["name"] = name == "topsolid_list_projects" ? "Manufacturing project" : "Company methods", ["pdmObjectId"] = name == "topsolid_list_projects" ? "project" : "library" });
                else if (name == "topsolid_list_pdm_children") {
                    if ((string?)args["pdmObjectId"] == "folder") rows = new JArray(Method());
                    else if (offset == 0) { rows = new JArray(JObject.Parse("{name:'Machining methods',pdmObjectId:'folder',kind:'folder'}")); more = true; }
                    else rows = new JArray(Method());
                } else throw new InvalidOperationException("Unexpected tool: " + name);
                data = new JObject { ["items"] = rows, ["offset"] = Fault == "page" ? 9 : offset, ["total"] = more ? 2 : offset + rows.Count, ["hasMore"] = more, ["nextOffset"] = more ? offset + rows.Count : null };
            }
            return Task.FromResult(new McpToolResult { StructuredContent = data });
        }
        public Task<JObject> PrepareToolAsync(string name, JObject args, CancellationToken token) => Task.FromResult(new JObject {
            ["toolName"] = Fault == "proposal" ? "wrong-tool" : name, ["arguments"] = args.DeepClone(), ["target"] = new JObject { ["documentId"] = "method-rev" }, ["confirmationToken"] = "ticket" });
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject args, string ticket, CancellationToken token)
        {
            Check.Equal("topsolid_open_document", name, "Unexpected mutation"); Check.Equal("ticket", ticket, "Wrong ticket"); Writes++;
            return Task.FromResult(new McpToolResult { StructuredContent = JObject.Parse("{opened:true,originalDocumentId:'method-rev',documentId:'method-rev'}") });
        }
    }
}
