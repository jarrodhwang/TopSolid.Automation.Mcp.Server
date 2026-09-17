using System;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // Connection ownership stays in the server; the desktop and contracts reference no vendor API.
    internal sealed partial class AutomationGateway : IDisposable
    {
        private const int ConnectionTimeoutSeconds = 5;
        private bool touchedHost;

        public JObject GetStatus()
        {
            try
            {
                EnsureConnected();
                return new JObject
                {
                    ["connected"] = true,
                    ["hostVersion"] = TopSolidHost.Application.Version,
                    ["hostVersionText"] = FormatVersion(TopSolidHost.Application.Version),
                    ["clientVersion"] = TopSolidHost.ClientVersion,
                    ["clientVersionText"] = FormatVersion(TopSolidHost.ClientVersion),
                    ["automationAssemblyVersion"] = typeof(TopSolidHost).Assembly.GetName().Version.ToString(),
                    ["message"] = "Connected to TopSolid; the Automation application service answered a live version query."
                };
            }
            catch (Exception ex)
            {
                Disconnect();
                return new JObject
                {
                    ["connected"] = false,
                    ["automationAssemblyVersion"] = typeof(TopSolidHost).Assembly.GetName().Version.ToString(),
                    ["message"] = "TopSolid Automation is unavailable. Open TopSolid in the same Windows session, wait until it is ready, and retry. TopSolid was not started by this server.",
                    ["detail"] = Describe(ex)
                };
            }
        }

        public JObject GetActiveDocument() { return ReadDocument(null, false); }
        public JObject GetDocumentInfo(string documentId) { return ReadDocument(documentId, true); }

        private JObject ReadDocument(string documentId, bool includeDetails)
        {
            try
            {
                EnsureConnected();
                var documents = TopSolidHost.Documents;
                if (documents == null) throw new InvalidOperationException("The TopSolid documents service is unavailable.");
                var id = documentId == null ? documents.EditedDocument : new DocumentId(documentId);
                if (id.IsEmpty)
                    return new JObject
                    {
                        ["connected"] = true, ["hasActiveDocument"] = false, ["document"] = JValue.CreateNull(),
                        ["message"] = "No document is currently being edited in TopSolid."
                    };
                if (!documents.Exists(id))
                    throw new InvalidOperationException("The requested document no longer exists. Query the active document again.");
                var document = new JObject { ["documentId"] = id.PdmDocumentId, ["name"] = documents.GetName(id) };
                if (includeDetails)
                {
                    document["typeFullName"] = documents.GetTypeFullName(id);
                    document["typeGuid"] = documents.GetTypeGuid(id).ToString("D");
                    document["isDirty"] = documents.IsDirty(id);
                    var pdmObject = TopSolidHost.Documents.GetPdmObject(id);
                    document["pdmObjectId"] = AutomationValues.Json(pdmObject);
                    TopSolidHost.Pdm.GetType(pdmObject, out var extension);
                    document["extension"] = extension;
                }
                var result = new JObject { ["connected"] = true, ["document"] = document };
                if (documentId == null) result["hasActiveDocument"] = true;
                return result;
            }
            catch
            {
                // Drop stale/faulted WCF channels. The next call attempts a fresh connection.
                Disconnect();
                throw;
            }
        }

        private void EnsureConnected()
        {
            touchedHost = true;
            if (!TopSolidHost.IsConnected)
            {
                // The Boolean return means "automatically started", NOT "connected".
                TopSolidHost.Connect(false, ConnectionTimeoutSeconds, "TopSolidAutomationMcp_" + Process.GetCurrentProcess().Id);
            }
            if (!TopSolidHost.IsConnected || TopSolidHost.Application == null)
                throw new InvalidOperationException("No connection to an existing TopSolid Automation host could be established.");
            if (TopSolidHost.Application.Version < 720000000)
                throw new InvalidOperationException("These tools require a ready TopSolid 7.20 or newer host.");
        }

        private void Disconnect()
        {
            if (!touchedHost) return;
            DisconnectModules();
            try { TopSolidHost.Disconnect(); }
            catch (Exception ex) { Console.Error.WriteLine("Automation disconnect: " + Describe(ex)); }
            touchedHost = false;
        }

        public void Dispose() { Disconnect(); }
        public static string FormatVersion(int version) => string.Format("{0}.{1}.{2}.{3}", version / 100000000, version / 1000000 % 100, version / 1000 % 1000, version % 1000);
        public static string Describe(Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
    }
}
