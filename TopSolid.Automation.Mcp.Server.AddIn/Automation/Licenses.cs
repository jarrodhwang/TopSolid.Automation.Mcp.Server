using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        public JObject GetLicenseStatus() => Read("kernel", () => JObject.FromObject(ReadLicenseStatus(
            TopSolidHost.Application.IsLicenseValid, () => TopSolidHost.Licenses.GetActveLicenses(),
            FormatVersion(TopSolidHost.Application.Version))));

        internal static TopSolidLicenseStatus ReadLicenseStatus(Func<int, bool> isValid,
            Func<IEnumerable<License>> activeLicenses, string hostVersion)
        {
            // Use the vendor's validity decision; Status is localized display text and a nullable
            // expiration date does not encode activation, maintenance or version entitlement rules.
            var valid = isValid(TopSolidLicenseStatus.KernelBaseModule);
            var snapshot = new TopSolidLicenseStatus
            {
                SchemaVersion = 1, RequiredModule = TopSolidLicenseStatus.KernelBaseModule,
                RequiredLicenseValid = valid, HostVersion = hostVersion,
                CheckedAtUtc = DateTimeOffset.UtcNow
            };
            try
            {
                var licenses = activeLicenses();
                if (licenses == null) return snapshot;
                var validity = new Dictionary<uint, bool?> { [TopSolidLicenseStatus.KernelBaseModule] = valid };
                foreach (var license in licenses)
                {
                    if (!validity.ContainsKey(license.Module))
                    {
                        // One read per distinct module; inability to inspect another product's
                        // entitlement must not override the authoritative Kernel Base result.
                        // Product packages can report Module=0. That is not a module entitlement;
                        // querying IsLicenseValid(0) would mislabel unrelated packages as valid.
                        try { validity[license.Module] = LicenseValidity(license.Module, isValid); }
                        catch { validity[license.Module] = null; }
                    }
                    snapshot.Licenses.Add(LicenseInfo(license, validity[license.Module]));
                }
                snapshot.DetailsAvailable = true;
            }
            catch
            {
                // Preserve the required module's result when only optional display metadata fails.
                snapshot.Licenses.Clear();
            }
            return snapshot;
        }

        internal static bool? LicenseValidity(uint module, Func<int, bool> isValid) =>
            module == 0 || module > int.MaxValue ? (bool?)null : isValid((int)module);

        internal static TopSolidLicenseInfo LicenseInfo(License license, bool? valid) => new TopSolidLicenseInfo
        {
            Name = license.Name, Module = license.Module, Version = license.Version,
            Type = license.LicenseType.ToString(), Active = license.IsActive, Valid = valid,
            ExpirationDate = license.ExpirationDate, LicenseUser = license.LicenseUser,
            LicensedTo = license.LicensedTo, Status = license.Status
        };
    }
}
