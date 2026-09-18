using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Licensing;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class LicenseTests
{
    internal static TopSolidLicenseStatus Fixture(bool? valid = true) => new()
    {
        SchemaVersion = 1, RequiredModule = 1000, RequiredLicenseValid = valid, HostVersion = "7.20.400.107",
        CheckedAtUtc = DateTimeOffset.UtcNow, DetailsAvailable = true,
        Licenses = [new() { Module = 1000, Name = "TopSolid'Kernel Base", Type = "Floating", Valid = valid,
            Active = valid == true, LicenseUser = "Workshop user", LicensedTo = "Workshop", Status = valid == true ? "Active" : "Expired",
            Version = "7.20", ExpirationDate = valid == true ? new DateTime(2027, 12, 31) : new DateTime(2020, 1, 1) }]
    };

    public static async Task Gate()
    {
        foreach (var status in new[] { Fixture(false), Fixture(null), new TopSolidLicenseStatus(),
            new TopSolidLicenseStatus { SchemaVersion = 1, RequiredModule = 7899, RequiredLicenseValid = true },
            new TopSolidLicenseStatus { SchemaVersion = 2, RequiredModule = 1000, RequiredLicenseValid = true } })
        {
            var launches = 0; var messages = 0;
            var dismissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var result = LicenseStartup.RunAsync(_ => Task.FromResult(status), _ => launches++, _ => { messages++; return dismissed.Task; }, CancellationToken.None);
            Check.True(!result.IsCompleted && launches == 0 && messages == 1, "Invalid/unverified startup did not wait for the reason dialog before exiting");
            dismissed.SetResult();
            Check.True(!await result && launches == 0, "A rejected license created the main window");
        }
        foreach (var failure in new Exception[] { new IOException("host unavailable"), new TimeoutException(), new InvalidOperationException("old server") })
        {
            var message = false;
            Check.True(!await LicenseStartup.RunAsync(_ => Task.FromException<TopSolidLicenseStatus>(failure), _ => throw new Exception("Unexpected launch"),
                status => { message = status.RequiredLicenseValid == null; return Task.CompletedTask; }, CancellationToken.None) && message, "Failed lookup did not close safely");
        }
        var calls = 0; var fresh = Fixture();
        fresh.Licenses[0].Name = "Localized or custom product name";
        fresh.Licenses[0].ExpirationDate = null;
        Check.True(await LicenseStartup.RunAsync(_ => Task.FromResult(fresh), status => { Check.True(ReferenceEquals(fresh, status), "License snapshot lost at handoff"); calls++; },
            _ => throw new Exception("Unexpected failure dialog"), CancellationToken.None) && calls == 1, "Vendor-confirmed module 1000 did not start exactly once");
        fresh.DetailsAvailable = false; fresh.Licenses.Clear();
        Check.True(fresh.CanStart, "Missing optional display fields incorrectly replaced TopSolid's validity decision");
        using var cancellation = new CancellationTokenSource();
        var late = new TaskCompletionSource<TopSolidLicenseStatus>();
        var pending = LicenseStartup.RunAsync(_ => late.Task, _ => throw new Exception("Late launch"), _ => throw new Exception("Dialog after cancellation"), cancellation.Token);
        cancellation.Cancel(); late.SetResult(Fixture());
        Check.True(!await pending, "Closing the progress window allowed a late successful query to reopen Studio");
    }

    public static async Task Transport()
    {
        var good = JObject.FromObject(Fixture());
        var cases = new List<(JObject Response, bool Accept)> { (good, true), (JObject.FromObject(Fixture(false)), true), (new JObject(), false) };
        foreach (var replacement in new JToken[] { new JValue("true"), new JValue(1), JValue.CreateNull() })
        { var bad = (JObject)good.DeepClone(); bad["requiredLicenseValid"] = replacement; cases.Add((bad, false)); }
        var wrong = (JObject)good.DeepClone(); wrong["requiredModule"] = 2000; cases.Add((wrong, false));
        var missing = (JObject)good.DeepClone(); missing.Remove("licenses"); cases.Add((missing, false));
        var nullRow = (JObject)good.DeepClone(); nullRow["licenses"] = new JArray(JValue.CreateNull()); cases.Add((nullRow, false));
        foreach (var (response, accept) in cases)
        {
            await using var client = new StdioMcpClient();
            var prior = Environment.GetEnvironmentVariable("TOPSOLID_MCP_TEST_FIXTURE");
            var priorResponse = Environment.GetEnvironmentVariable("TOPSOLID_LICENSE_TEST_RESPONSE");
            try
            {
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_TEST_FIXTURE", "1");
                Environment.SetEnvironmentVariable("TOPSOLID_LICENSE_TEST_RESPONSE", response.ToString(Newtonsoft.Json.Formatting.None));
                await client.ConnectAsync(Path.Combine(AppContext.BaseDirectory, "TopSolid.Automation.Tests.exe"), CancellationToken.None);
            }
            finally
            {
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_TEST_FIXTURE", prior);
                Environment.SetEnvironmentVariable("TOPSOLID_LICENSE_TEST_RESPONSE", priorResponse);
            }
            if (accept)
            {
                var result = await client.GetLicenseStatusAsync(CancellationToken.None);
                Check.Equal((bool)response["requiredLicenseValid"]!, result.CanStart, "Transport lost native validity");
                Check.Equal("Workshop user", result.Licenses[0].LicenseUser, "License user was lost");
                Check.Equal("7.20", result.Licenses[0].Version, "License version was lost");
                Check.True(result.Licenses[0].ExpirationDate.HasValue, "License expiration was lost");
            }
            else await Check.ThrowsAsync<IOException>(() => client.GetLicenseStatusAsync(CancellationToken.None));
        }
    }

    public static async Task Live(string executable)
    {
        await using var client = new StdioMcpClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await client.ConnectAsync(executable, deadline.Token);
        var status = await client.GetLicenseStatusAsync(deadline.Token);
        // Deliberately omit license user/owner, keys and raw license records from diagnostics.
        Console.WriteLine($"Kernel Base 1000: valid={status.RequiredLicenseValid}; host={status.HostVersion}; details={status.DetailsAvailable}; modules={status.Licenses.Count}; checked={status.CheckedAtUtc:O}");
        var required = status.Licenses.Where(l => l.Module == 1000).ToList();
        Console.WriteLine($"Kernel Base records={required.Count}; active={required.Any(l => l.Active)}; expirationProvided={required.Any(l => l.ExpirationDate != null)}; userProvided={required.Any(l => !string.IsNullOrWhiteSpace(l.LicenseUser))}");
        foreach (var license in status.Licenses)
            Console.WriteLine($"Module={license.Module}; name={license.Name}; type={license.Type}; active={license.Active}; valid={license.Valid}; expirationProvided={license.ExpirationDate != null}");
    }
}
