using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ApiRefs
    {
        private const string LocalRootName = "TopSolid.Automation";
        private const string OfficialBaseUrl = "https://help.topsolid.com/7.20/en/TopSolid'Automation/";
        private const string OfficialEncodedBaseUrl = "https://help.topsolid.com/7.20/en/TopSolid%27Automation/";
        private static readonly Lazy<Dictionary<string, string>> LocalPaths =
            new Lazy<Dictionary<string, string>>(LoadLocalPaths);

        public static string[] For(string module, string ns, params string[] symbols) => symbols.Select(symbol =>
            LocalPathForHref("api/" + module + "/" + ns + "." + symbol + ".html")).ToArray();
        public static string[] Kernel(params string[] symbols) => For("kernel", "TopSolid.Kernel.Automating", symbols);
        public static string[] Cam(params string[] symbols) => For("cam", "TopSolid.Cam.NC.Kernel.Automating", symbols);

        public static string LocalPathForHref(string href)
        {
            var normalized = Normalize(href);
            string localPath;
            if (LocalPaths.Value.TryGetValue(normalized, out localPath)) return localPath;
            return LocalRootName + "/embedded/" + normalized;
        }

        public static string LocalPathForReference(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return reference;
            if (reference.StartsWith(OfficialBaseUrl, StringComparison.OrdinalIgnoreCase))
                return LocalPathForHref(reference.Substring(OfficialBaseUrl.Length));
            if (reference.StartsWith(OfficialEncodedBaseUrl, StringComparison.OrdinalIgnoreCase))
                return LocalPathForHref(reference.Substring(OfficialEncodedBaseUrl.Length));
            return reference;
        }

        public static string LocalPathForEntry(JToken entry)
        {
            var relative = entry == null ? null : (string)entry["markdown"] ?? (string)entry["article"];
            return string.IsNullOrWhiteSpace(relative)
                ? LocalPathForHref(entry == null ? "" : (string)entry["href"])
                : ToLocalPath(relative);
        }

        private static Dictionary<string, string> LoadLocalPaths()
        {
            JObject index;
            var localIndexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LocalRootName, "reference-index.json");
            if (File.Exists(localIndexPath))
            {
                index = JObject.Parse(File.ReadAllText(localIndexPath));
            }
            else
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TopSolid.ApiReference.json"))
                {
                    if (stream == null) return new Dictionary<string, string>(StringComparer.Ordinal);
                    using (var reader = new StreamReader(stream)) index = JObject.Parse(reader.ReadToEnd());
                }
            }

            return ((JArray)index["entries"] ?? new JArray())
                .Cast<JObject>()
                .Select(entry => new
                {
                    Href = Normalize((string)entry["href"]),
                    Relative = (string)entry["markdown"] ?? (string)entry["article"]
                })
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Href) && !string.IsNullOrWhiteSpace(entry.Relative))
                .GroupBy(entry => entry.Href, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => ToLocalPath(group.First().Relative), StringComparer.Ordinal);
        }

        private static string ToLocalPath(string relative)
        {
            var normalized = Normalize(relative);
            return normalized.StartsWith(LocalRootName + "/", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : LocalRootName + "/" + normalized;
        }

        private static string Normalize(string path) => (path ?? "").Replace('\\', '/').TrimStart('/');
    }
}
