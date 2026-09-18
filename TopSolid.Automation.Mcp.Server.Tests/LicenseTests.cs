using System;
using System.Collections.Generic;
using System.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void Licenses()
        {
            var queried = new List<int>();
            var package = new License(LicenseType.Standalone, "fixture", "TopSolid'Design Pro", "7.20", true, "", 0, 100);
            var module = new License(LicenseType.Floating, "fixture", "Localized Kernel Base name", "7.20", true, "Fixture user", 1000, 0)
                { ExpirationDate = new DateTime(2027, 12, 31), LicensedTo = "Fixture owner", Status = "Localized status" };
            var snapshot = AutomationGateway.ReadLicenseStatus(id => { queried.Add(id); return id == 1000; },
                () => new[] { package, module, module }, "7.20.400.107");
            Check(snapshot.CanStart && queried.SequenceEqual(new[] { 1000 }), "Required validity must be queried once and never inferred from package Module=0");
            Check(snapshot.Licenses[0].Valid == null, "Package was incorrectly given another module's validity");
            Check(snapshot.Licenses[1].ExpirationDate == module.ExpirationDate && snapshot.Licenses[1].LicenseUser == "Fixture user" &&
                snapshot.Licenses[1].Status == "Localized status" && snapshot.Licenses[1].Active, "Native license display facts changed in transport");
            snapshot = AutomationGateway.ReadLicenseStatus(_ => false, () => new[] { module }, "7.20.400.107");
            Check(!snapshot.CanStart && snapshot.Licenses[0].Valid == false, "Active or future-dated metadata overrode the vendor's invalid decision");
            snapshot = AutomationGateway.ReadLicenseStatus(_ => true, () => new[] { package }, "7.20.400.107");
            Check(snapshot.CanStart && snapshot.DetailsAvailable, "A packaged entitlement was rejected because Kernel Base is not separately listed");
            snapshot = AutomationGateway.ReadLicenseStatus(_ => true, () => { throw new Exception("metadata unavailable"); }, "7.20.400.107");
            Check(snapshot.CanStart && !snapshot.DetailsAvailable && snapshot.Licenses.Count == 0, "Optional metadata failure overwrote a verified entitlement");
            Throws<Exception>(() => AutomationGateway.ReadLicenseStatus(_ => { throw new Exception("unverified"); }, () => new[] { module }, "7.20.400.107"));
            Check(AutomationGateway.LicenseValidity(uint.MaxValue, _ => throw new Exception("Invalid module reached SDK")) == null, "Out-of-range module was not treated as unknown");
        }
    }
}
