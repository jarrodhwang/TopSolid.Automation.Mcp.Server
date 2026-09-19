using System;
using System.Text.RegularExpressions;

namespace TopSolid.Automation.Mcp.Contracts
{
    // Contains no credentials. One MCP process owns one immutable TopSolid target.
    public sealed class TopSolidConnectionOptions
    {
        public const string EnvironmentName = "TOPSOLID_CONNECTION";
        public const string TokenEnvironmentName = "TOPSOLID_CONNECTION_TOKEN";
        public string Mode { get; set; } = "local";
        public string Selection { get; set; } = "auto";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 443;
        public string PipeName { get; set; } = "";
        public string ExpectedVersion { get; set; } = "";
        public int ProcessId { get; set; }
        public long StartTimeUtcTicks { get; set; }

        public void Validate()
        {
            if (Mode != "local" && Mode != "tcp" && Mode != "https") throw new ArgumentException("Choose a TopSolid connection mode.");
            if (Selection != "auto" && Selection != "instance" && Selection != "pipe") throw new ArgumentException("Choose an instance selection mode.");
            if (Mode != "local")
            {
                if (string.IsNullOrWhiteSpace(Host) || Host != Host.Trim() || Host.Length > 253 ||
                    Uri.CheckHostName(Host) == UriHostNameType.Unknown || Host.IndexOfAny(new[] { '/', '\\', '@', ':', '?', '#' }) >= 0)
                    throw new ArgumentException("Enter a host name or IPv4 address without a URL or port.");
                if (Port < 1 || Port > 65535) throw new ArgumentException("Port must be between 1 and 65535.");
            }
            if (PipeName == null || PipeName.Length > 100 || (PipeName.Length > 0 && !Regex.IsMatch(PipeName, @"\A[a-zA-Z0-9_.-]+\z")))
                throw new ArgumentException("Pipe name may contain letters, numbers, dots, underscores and hyphens.");
            if (ExpectedVersion == null || (ExpectedVersion.Length > 0 && !Regex.IsMatch(ExpectedVersion, @"\A[0-9]{1,2}\.[0-9]{1,2}\z")))
                throw new ArgumentException("Enter a TopSolid version such as 7.20.");
            if (ProcessId < 0 || StartTimeUtcTicks < 0) throw new ArgumentException("Invalid instance identity.");
            if (Mode != "tcp" && Selection == "instance" && (ProcessId == 0 || StartTimeUtcTicks == 0))
                throw new ArgumentException("Refresh the instance list and select a running TopSolid instance.");
            if (Mode != "tcp" && Selection == "pipe" && string.IsNullOrWhiteSpace(PipeName))
                throw new ArgumentException("Enter the pipe name configured for the TopSolid instance.");
        }

        public Uri GatewayUri() => new UriBuilder("wss", Host, Port, "/topsolid/").Uri;
    }

    public sealed class TopSolidInstanceInfo
    {
        public int ProcessId { get; set; }
        public long StartTimeUtcTicks { get; set; }
        public string Version { get; set; } = "";
        public string Title { get; set; } = "";
        public string PipeName { get; set; } = "";
        public bool PipeKnown { get; set; }
        public bool Supported { get; set; }
        public string DisplayName => "TopSolid " + Version + " · PID " + ProcessId + (Title.Length == 0 ? "" : " · " + Title);
    }
}
