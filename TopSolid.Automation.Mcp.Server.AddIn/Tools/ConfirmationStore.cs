using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    // A trusted MCP client obtains this ticket outside the model's tool channel.
    // It is single-use, short-lived, and bound to the exact proposed arguments.
    internal sealed class ConfirmationStore
    {
        private sealed class Proposal
        {
            public string Name;
            public JObject Arguments;
            public JObject Target;
            public DateTime Expires;
        }
        private readonly Dictionary<string, Proposal> proposals = new Dictionary<string, Proposal>(StringComparer.Ordinal);
        private readonly Func<DateTime> utcNow;
        public ConfirmationStore(Func<DateTime> utcNow = null) { this.utcNow = utcNow ?? (() => DateTime.UtcNow); }

        public JObject Prepare(ToolDefinition tool, JObject arguments, JObject target)
        {
            foreach (var key in proposals.Where(p => p.Value.Expires <= utcNow()).Select(p => p.Key).ToArray()) proposals.Remove(key);
            if (proposals.Count >= 16) throw new RpcException(-32010, "Too many pending confirmations. Wait two minutes and prepare again.");
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var token = Convert.ToBase64String(bytes);
            var expires = utcNow().AddMinutes(2);
            proposals.Add(token, new Proposal { Name = tool.Name, Arguments = (JObject)arguments.DeepClone(), Target = (JObject)target.DeepClone(), Expires = expires });
            return new JObject
            {
                ["confirmationToken"] = token, ["expiresAt"] = expires.ToString("O"),
                ["toolName"] = tool.Name, ["description"] = tool.Definition["description"].DeepClone(),
                ["target"] = target.DeepClone(), ["arguments"] = arguments.DeepClone(),
                ["inputLengthUnits"] = (string)arguments["units"] ?? tool.DefaultLengthUnits ?? "Not applicable",
                ["defaults"] = tool.Defaults ?? "Only the exact arguments shown are approved.",
                ["effect"] = tool.Effect
            };
        }
        public JObject Consume(string token, string name, JObject arguments)
        {
            if (token == null || !proposals.TryGetValue(token, out var proposal))
                throw new RpcException(-32010, "Explicit user confirmation is required. The client must prepare and approve this exact change.");
            proposals.Remove(token); // Consume even a mismatched or expired ticket; never reuse it.
            if (proposal.Expires <= utcNow() || proposal.Name != name || !JToken.DeepEquals(proposal.Arguments, arguments))
                throw new RpcException(-32010, "Confirmation expired or does not match this change. Prepare a new preview and ask the user again.");
            return (JObject)proposal.Target.DeepClone();
        }
    }
}
