using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // Extension identifiers supplied for this installation on 2026-09-16.
    // Independent of loaded documents; this catalog is not a license check.
    internal static class DocumentCreationCatalog
    {
        public const string PartExtension = ".TopPrt";
        private const string NativeExtensions = ".Top3D;.TopRpd;.Top3DFct;.TopPipRls;.TopMacComp;.TopFamDrv;.TopNCOpConfig;.TopPrtPcs;.TopPla;.TopCadmouldRes;.TopStd;.TopPreset;.TopClh;.TopFeatToolNb;.TopCylRls;.TopUserParams;.TopDic;.TopMac;.TopNest;.TopWizRls;.TopMulMat;.TopWkSBkg;.Top2D;.TopDftBdl;.TopCadmouldDat;.TopBom;.TopCTopo;.TopPrtFct;.TopWMWork;.TopNCDummy;.TopAsmFct;.TopMAssoc;.TopDrw;.TopDft3D;.TopStrip;.TopMillTurnMethod;.TopFam;.TopMachLink;.TopMet;.TopWork;.TopMillTurn;.TopProgress;.TopEnu;.TopSplit;.TopNcf;.TopPrtSet;.TopPrt;.TopFct;.TopMatEqu;.TopTex;.TopWkSMan;.TopPeck;.TopUbdPars;.TopExpld;.TopWMTask;.TopBoxRls;.TopSet;.TopCla;.TopCustomLead;.TopWPP;.TopNewPrtSet;.TopMold;.TopUnfld;.TopEld;.TopDft;.TopCamConfig;.TopPdf;.TopCoa;.TopPcsRls;.TopStcCstRls;.TopMat;.TopWMConfig;.TopPrd;.TopThr;.Top3DWiz;.TopTol;.TopFdCfg;.TopEnv;.TopToolCat;.TopFlt;.TopMacProcess;.TopWMSupport;.TopFeatPostPro;.TopCAERes;.TopPartPositRules;.TopFin;.TopLnk;.TopCAEDat;.TopNcLog;.TopSpf;.TopCut;.Top2DFct;.TopAsm;.TopCCool";
        private static readonly string[] Native = NativeExtensions.Split(';');
        private static readonly string[] External = { ".bin", ".pdf", ".png", ".topfud", ".txt", ".xml" };

        public static string Extension(string value)
        {
            if (value == null || !Regex.IsMatch(value, @"\A\.[A-Za-z][A-Za-z0-9]{1,62}\z"))
                throw new ArgumentException("Supply one extension including the dot, for example .TopPrt. Paths and extension lists are not accepted.");
            if (string.Equals(value, ".TopPrj", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(".TopPrj is a project. Use topsolid_create_project(name), not document creation.");
            if (External.Contains(value, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException(value + " is an external-file format, not an empty native document. Import requires a source file and a suitable import tool; it must not use IPdm.CreateDocument.");
            // Explicit native types from a user/live query may exceed this seed catalog.
            if (value.Length <= 4 || !value.StartsWith(".Top", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Native creation requires a .Top... extension from the catalog, user or TopSolid.");
            return Native.FirstOrDefault(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)) ?? value;
        }
        public static JObject Describe(string value)
        {
            var extension = Extension(value);
            return new JObject { ["extension"] = extension, ["creationMode"] = "empty", ["useDefaultTemplate"] = false,
                ["loadedDocumentRequired"] = false, ["source"] = Native.Contains(extension) ? "installation extension catalog supplied by user" : "explicit native extension; host validates availability",
                ["availability"] = "Not a runtime module/license check. TopSolid validates creation after confirmation." };
        }
        public static JArray Entries() => new JArray(Native.Select(v => new JObject { ["extension"] = v, ["route"] = "topsolid_create_document", ["useDefaultTemplate"] = false })
            .Concat(new[] { new JObject { ["extension"] = ".TopPrj", ["route"] = "topsolid_create_project" } })
            .Concat(External.Select(v => new JObject { ["extension"] = v, ["route"] = "source-file import required; not empty native creation" })));
        public static JObject PartArguments(JObject input)
        {
            var result = (JObject)input.DeepClone(); result["extension"] = PartExtension; result["useDefaultTemplate"] = false; return result;
        }
        public static bool UseDefaultTemplate(JObject input) => (bool?)input["useDefaultTemplate"] ?? false;
        public static void RequireIdle(string commandName)
        {
            if (!string.IsNullOrEmpty(commandName)) throw new InvalidOperationException("TopSolid has an active command: " + commandName + ". Finish or cancel it in TopSolid, then request a fresh preview. No creation was attempted; no command was canceled automatically.");
        }
    }
}
