using System;
using System.Linq;
using Newtonsoft.Json;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed class ConnectionTarget
    {
        internal readonly TopSolidConnectionOptions Options;
        private TopSolidInstanceInfo pinned;
        private int connectedProcessId;

        internal ConnectionTarget(TopSolidConnectionOptions options) { Options = options; options.Validate(); }
        internal static TopSolidConnectionOptions FromEnvironment()
        {
            var json = Environment.GetEnvironmentVariable(TopSolidConnectionOptions.EnvironmentName);
            if (json != null && json.Length > 8192) throw new ArgumentException("Connection settings are too large.");
            var result = json == null ? new TopSolidConnectionOptions() : JsonConvert.DeserializeObject<TopSolidConnectionOptions>(json);
            if (result == null) throw new ArgumentException("Missing connection settings.");
            result.Validate(); return result;
        }
        internal void Configure()
        {
            if (Options.Mode == "tcp") { TopSolidHost.DefineConnection(Options.Host, Options.Port, null, 0); return; }
            if (Options.Mode != "local") throw new InvalidOperationException("HTTPS requires the gateway relay.");
            if (Options.Selection == "pipe") TopSolidHost.PipeName = Options.PipeName;
            else
            {
                var candidates = TopSolidInstances.Discover();
                if (pinned == null)
                {
                    var matches = candidates.Where(i => Options.Selection == "instance" ?
                        i.ProcessId == Options.ProcessId && i.StartTimeUtcTicks == Options.StartTimeUtcTicks :
                        Options.ExpectedVersion.Length == 0 || i.Version == Options.ExpectedVersion).ToList();
                    if (matches.Count != 1) throw new InvalidOperationException("Refresh the instance list and select one running TopSolid instance.");
                    pinned = matches[0];
                }
                if (!candidates.Any(i => i.ProcessId == pinned.ProcessId && i.StartTimeUtcTicks == pinned.StartTimeUtcTicks))
                    throw new InvalidOperationException("The selected instance has exited. Refresh the instance list and select it again.");
                if (!pinned.Supported) throw new InvalidOperationException("These tools require TopSolid 7.18 or newer. Older versions are shown for identification only.");
                if (!pinned.PipeKnown) throw new InvalidOperationException("The instance pipe could not be read. Enter its pipe name or use its Automation TCP port.");
                TopSolidHost.PipeName = pinned.PipeName.Length == 0 ? null : pinned.PipeName;
            }
            TopSolidHost.DefineConnection(null, 0, null, 0);
        }
        internal void Verify(int processId, int version)
        {
            if ((pinned != null && processId != pinned.ProcessId) || (Options.Mode == "tcp" && Options.ProcessId > 0 && processId != Options.ProcessId) ||
                (connectedProcessId != 0 && processId != connectedProcessId))
                throw new InvalidOperationException("A different TopSolid instance answered. Configure a unique -pipeName or Automation TCP port for the selected instance.");
            var text = version / 100000000 + "." + version / 1000000 % 100;
            if (Options.ExpectedVersion.Length > 0 && Options.ExpectedVersion != text)
                throw new InvalidOperationException("The connected TopSolid version does not match the selected version.");
            connectedProcessId = processId;
        }
    }
}
