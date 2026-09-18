using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TopSolid.Automation.Mcp.Contracts
{
    // Read-only facts from TopSolid. Missing dates or display metadata never imply a perpetual license.
    public sealed class TopSolidLicenseStatus
    {
        public const int KernelBaseModule = 1000;
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonProperty("requiredModule")] public int RequiredModule { get; set; }
        [JsonProperty("requiredLicenseValid")] public bool? RequiredLicenseValid { get; set; }
        [JsonProperty("hostVersion")] public string HostVersion { get; set; } = "";
        [JsonProperty("checkedAtUtc")] public DateTimeOffset CheckedAtUtc { get; set; }
        [JsonProperty("detailsAvailable")] public bool DetailsAvailable { get; set; }
        [JsonProperty("licenses")] public List<TopSolidLicenseInfo> Licenses { get; set; } = new List<TopSolidLicenseInfo>();
        [JsonIgnore] public bool CanStart => SchemaVersion == 1 && RequiredModule == KernelBaseModule && RequiredLicenseValid == true;
    }

    public sealed class TopSolidLicenseInfo
    {
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("module")] public uint Module { get; set; }
        [JsonProperty("version")] public string Version { get; set; } = "";
        [JsonProperty("type")] public string Type { get; set; } = "";
        [JsonProperty("active")] public bool Active { get; set; }
        [JsonProperty("valid")] public bool? Valid { get; set; }
        [JsonProperty("expirationDate")] public DateTime? ExpirationDate { get; set; }
        [JsonProperty("licenseUser")] public string LicenseUser { get; set; } = "";
        [JsonProperty("licensedTo")] public string LicensedTo { get; set; } = "";
        [JsonProperty("status")] public string Status { get; set; } = "";
    }
}
