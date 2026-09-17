using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal sealed class ToolDefinition
    {
        public ToolDefinition(string name, string description, JObject properties, Func<JObject, JObject> execute,
            string category = "System", string[] required = null, bool readOnly = true, string[] api = null, Action<JObject> validate = null,
            Func<JObject, JObject> preview = null, string effect = null, string defaults = null, string defaultLengthUnits = null,
            Func<JObject, JObject, JObject> executePrepared = null)
        {
            Name = name;
            Execute = execute;
            ExecutePrepared = executePrepared;
            ReadOnly = readOnly;
            Category = category;
            ValidateArguments = validate;
            Preview = preview;
            Effect = effect ?? "Modify the specified document in one undoable modification. Changes are not saved automatically.";
            Defaults = defaults;
            DefaultLengthUnits = defaultLengthUnits;
            Definition = new JObject
            {
                ["name"] = name, ["description"] = description,
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object", ["properties"] = properties,
                    ["required"] = new JArray(required ?? new string[0]), ["additionalProperties"] = false
                },
                ["annotations"] = new JObject
                {
                    ["readOnlyHint"] = readOnly, ["destructiveHint"] = !readOnly,
                    ["idempotentHint"] = readOnly, ["openWorldHint"] = false
                },
                ["_meta"] = new JObject { ["topsolid/category"] = category,
                    ["topsolid/api"] = new JArray((api ?? new string[0])
                        .Concat(!readOnly && category != "Pdm" ? ApiRefs.Kernel("IDocuments.IsSynchronized", "IDocuments.GetSynchronizedDocuments", "IDocuments.GetPdmObject", "IDocuments.GetPdmMinorRevision") : new string[0])
                        .Select(ApiRefs.LocalPathForReference)
                        .Distinct()),
                    ["topsolid/requiresConfirmation"] = !readOnly }
            };
        }
        public string Name { get; }
        public string Category { get; }
        public bool ReadOnly { get; }
        public JObject Definition { get; }
        public Func<JObject, JObject> Execute { get; }
        public Func<JObject, JObject, JObject> ExecutePrepared { get; }
        public Action<JObject> ValidateArguments { get; }
        public Func<JObject, JObject> Preview { get; }
        public string Effect { get; }
        public string Defaults { get; }
        public string DefaultLengthUnits { get; }
    }
}
