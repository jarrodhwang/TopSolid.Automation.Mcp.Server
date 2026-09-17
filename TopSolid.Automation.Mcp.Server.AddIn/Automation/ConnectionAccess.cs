using System;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolid.Cad.Drafting.Automating;
using TopSolid.Cad.Electrode.Automating;
using TopSolid.Cam.NC.Kernel.Automating;
using TopSolid.Cae.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        public JObject Read(string module, Func<JObject> read)
        {
            try { ConnectModule(module); return read(); }
            catch (System.ServiceModel.CommunicationException) { Disconnect(); throw; }
            catch (TimeoutException) { Disconnect(); throw; }
        }
        public void ConnectModule(string module)
        {
            EnsureConnected();
            switch (module)
            {
                case "kernel": break;
                case "cad": if (!TopSolidDesignHost.IsConnected) TopSolidDesignHost.Connect(); break;
                case "drafting": if (!TopSolidDraftingHost.IsConnected) TopSolidDraftingHost.Connect(); break;
                case "cam": if (!TopSolidCamHost.IsConnected) TopSolidCamHost.Connect(); break;
                case "electrode": if (!TopSolidElectrodeHost.IsConnected) TopSolidElectrodeHost.Connect(); break;
                case "cae": if (!TopSolidCaeHost.IsConnected) TopSolidCaeHost.Connect(); break;
                default: throw new InvalidOperationException("No connected module adapter for " + module);
            }
        }
        public DocumentId Document(JObject arguments, string key = "documentId")
        {
            var id = arguments[key] == null ? TopSolidHost.Documents.EditedDocument : new DocumentId((string)arguments[key]);
            if (id.IsEmpty) throw new InvalidOperationException("No active document. Open a document or supply an ID returned by topsolid_list_documents.");
            if (!TopSolidHost.Documents.Exists(id)) throw new InvalidOperationException("The document revision no longer exists. Refresh document information.");
            return id;
        }
        public ElementId Element(JObject arguments, string key = "element")
        {
            var input = (JObject)arguments[key];
            var element = new ElementId(Document(input), (int)input["id"]);
            if (!TopSolidHost.Elements.Exists(element)) throw new InvalidOperationException("The element no longer exists. Query the document again.");
            return element;
        }
        public PdmObjectId Pdm(JObject arguments, string key = "pdmObjectId")
        {
            var id = new PdmObjectId((string)arguments[key]);
            if (id.IsEmpty || !TopSolidHost.Pdm.Exists(id)) throw new InvalidOperationException("The PDM object does not exist in the connected PDM.");
            return id;
        }
        public ElementExId CamElement(JObject arguments)
        {
            if (arguments["preparationId"] != null)
            {
                if (arguments["element"] != null || !Guid.TryParse((string)arguments["preparationId"], out var id) || id == Guid.Empty)
                    throw new ArgumentException("Supply one valid preparationId or one element, not both.");
                return new ElementExId(id);
            }
            if (arguments["element"] == null) throw new ArgumentException("An element or preparationId is required.");
            return new ElementExId(Element(arguments));
        }
        public ElementItemId Item(JObject arguments)
        {
            var item = (JObject)arguments["item"];
            var label = item["label"];
            return new ElementItemId(Element(item), AutomationValues.ParseItemLabel(label));
        }
        public JObject DocumentSummary(DocumentId id) => new JObject { ["documentId"] = id.PdmDocumentId, ["name"] = TopSolidHost.Documents.GetName(id) };
        private void DisconnectModules()
        {
            Action[] actions = {
                () => { if (TopSolidCaeHost.IsConnected) TopSolidCaeHost.Disconnect(); },
                () => { if (TopSolidElectrodeHost.IsConnected) TopSolidElectrodeHost.Disconnect(); },
                () => { if (TopSolidCamHost.IsConnected) TopSolidCamHost.Disconnect(); },
                () => { if (TopSolidDraftingHost.IsConnected) TopSolidDraftingHost.Disconnect(); },
                () => { if (TopSolidDesignHost.IsConnected) TopSolidDesignHost.Disconnect(); }
            };
            foreach (var action in actions) try { action(); } catch (Exception ex) { Console.Error.WriteLine("Module disconnect: " + ex.GetType().Name); }
        }
    }
}
