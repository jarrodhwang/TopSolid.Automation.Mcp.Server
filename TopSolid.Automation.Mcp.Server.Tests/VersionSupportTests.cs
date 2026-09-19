using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void VersionSupport()
        {
            Check(TopSolidVersionSupport.Display(718400283) == "7.18", "7.18 display version changed");
            Check(TopSolidVersionSupport.DisplayFull(718400283) == "7.18.400.283", "Full TopSolid version decoding changed");

            var calls = 0;
            var supported = new ToolDefinition("supported", "Supported test tool", new JObject(), _ => { calls++; return new JObject { ["ok"] = true }; });
            var module = new ToolDefinition("module", "7.20 module test tool", new JObject(), _ => { calls++; return new JObject { ["ok"] = true }; }, "Cae");
            var registry = new ToolRegistry(new[] { supported, module }, _ => new JObject(), hostVersion: () => TopSolidVersionSupport.MinimumSupportedVersion);

            var allowed = registry.Call("supported", new JObject());
            Check(!(bool)allowed["isError"] && calls == 1, "7.18 tool floor rejected a supported call");
            var blocked = registry.Call("module", new JObject());
            var payload = JObject.Parse((string)blocked["content"][0]["text"]);
            Check((bool)blocked["isError"] && (bool)payload["unsupportedVersion"] &&
                (string)payload["minimumVersion"] == "7.20" && (string)payload["connectedVersion"] == "7.18" && calls == 1,
                "Tool version guard did not block a 7.20 module on 7.18");
            var prepare = Throws<RpcException>(() => registry.Prepare("module", new JObject()));
            Check(prepare.Code == -32018 && prepare.Message.Contains("unsupportedVersion"), "Prepare did not return the structured version error");
            Console.WriteLine("PASS per-tool TopSolid version floors and 7.18 compatibility.");
        }
    }
}
