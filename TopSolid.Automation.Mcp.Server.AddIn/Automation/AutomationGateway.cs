using System;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // Connection ownership stays in the server; the desktop and contracts reference no vendor API.
    internal sealed partial class AutomationGateway : IDisposable
    {
        private const int ConnectionTimeoutSeconds = 5;
        private bool touchedHost;
        private readonly ConnectionTarget connectionTarget;
        public AutomationGateway() : this(new TopSolid.Automation.Mcp.Contracts.TopSolidConnectionOptions()) { }
        internal AutomationGateway(TopSolid.Automation.Mcp.Contracts.TopSolidConnectionOptions options) { connectionTarget = new ConnectionTarget(options); }

        public JObject GetStatus()
        {
            try
            {
                EnsureConnected();
                return new JObject
                {
                    ["connected"] = true,
                    ["processId"] = TopSolidHost.Application.ProcessId,
                    ["connection"] = JObject.FromObject(connectionTarget.Options),
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
                    ["message"] = "Check the selected TopSolid instance and connection settings.",
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
                var document = new JObject { ["documentId"] = id.PdmDocumentId, ["name"] = documents.GetName(id), ["typeFullName"] = documents.GetTypeFullName(id) };
                if (includeDetails)
                {
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
                connectionTarget.Configure();
                // The Boolean return means "automatically started", NOT "connected".
                TopSolidHost.Connect(false, ConnectionTimeoutSeconds, "TopSolidAutomationMcp_" + Process.GetCurrentProcess().Id);
            }
            if (!TopSolidHost.IsConnected || TopSolidHost.Application == null)
                throw new InvalidOperationException("No connection to an existing TopSolid Automation host could be established.");
            if (TopSolidHost.Application.Version < TopSolidVersionSupport.MinimumSupportedVersion)
                throw new InvalidOperationException("These tools require a ready TopSolid 7.18 or newer host.");
            try { connectionTarget.Verify(TopSolidHost.Application.ProcessId, TopSolidHost.Application.Version); }
            catch { Disconnect(); throw; }
        }

        internal int GetConnectedHostVersion()
        {
            EnsureConnected();
            return TopSolidHost.Application.Version;
        }

        private void Disconnect()
        {
            if (!touchedHost) return;
            DisconnectModules();
            try { TopSolidHost.Disconnect(); }
            catch (Exception ex) { Console.Error.WriteLine("Automation disconnect: " + Describe(ex)); }
            touchedHost = false;
        }

        public void Dispose() { previewTransfers.Dispose(); Disconnect(); }
        public static string FormatVersion(int version) => string.Format("{0}.{1}.{2}.{3}", version / 100000000, version / 1000000 % 100, version / 1000 % 1000, version % 1000);
        public static string Describe(Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
    }
}
