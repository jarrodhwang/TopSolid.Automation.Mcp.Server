using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Contracts
{
    // Protocol data only: never reference TopSolid assemblies from this project.
    public sealed class McpToolDefinition
    {
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("inputSchema")] public JObject InputSchema { get; set; } = new JObject();
        [JsonProperty("annotations")] public JObject Annotations { get; set; } = new JObject();
        [JsonProperty("_meta")] public JObject Metadata { get; set; } = new JObject();
        [JsonIgnore] public bool RequiresConfirmation => (bool?)Annotations["readOnlyHint"] != true;
    }

    public sealed class McpToolResult
    {
        [JsonProperty("content")] public JArray Content { get; set; } = new JArray();
        [JsonProperty("isError")] public bool IsError { get; set; }
        [JsonProperty("structuredContent", NullValueHandling = NullValueHandling.Ignore)]
        public JObject? StructuredContent { get; set; }

        public static McpToolResult Error(string message) => new McpToolResult
        {
            IsError = true,
            Content = new JArray(new JObject { ["type"] = "text", ["text"] = message })
        };
    }
}
