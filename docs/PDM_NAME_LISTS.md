# PDM friendly-name listing — 0.3.2

`topsolid_list_projects` and `topsolid_list_libraries` now return each item's **name and pdmObjectId** in the same page. The server resolves names using the documented [IPdm.GetName](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.GetName.html) method only for the requested page. `total`, `offset`, `limit` and `hasMore` remain unchanged. Consumers of the previous string-only `items` must now use `item.pdmObjectId`.

The model no longer needs a separate `topsolid_get_pdm_object_info` call for each name. These two tools default to 100 entries per page; other paged tools retain their default of 25. Explicit smaller limits remain supported. Tool descriptions and chat instructions request pages of 100 for all-name queries, follow `hasMore`, preserve friendly names and separate working projects from libraries. Existing tool-call limits remain enforced.

## Gemini diagnosis

The user's log contained a name-lookup fan-out exceeding 32 calls. Direct name projection resolves that application defect.

The separate 404 was an account/model availability issue. Google's actual response for the normalized `gemini-2.5-flash` request said the model was no longer available to new users and recommended `gemini-3.6-flash`. Model discovery still included the older model, so listing alone did not prove inference access. Studio now displays bounded, credential-redacted provider 404 messages, including Google's array-wrapped errors.

Gemini discovery and requests both strip the native `models/` prefix, while other providers' model IDs are preserved. This normalization does not bypass access restrictions. No automatic model substitution occurs in the application.

## Verified live result

- Native PDM: **50 working projects and 66 libraries**, with every name/ID returned. Reads forced 17-item pages to verify completeness, totals and no duplicate IDs. Duplicate friendly names are preserved because different projects can share a name.
- Real Gemini **3.6 Flash → MCP → TopSolid → final answer**: **116/116 native names present**, using **two MCP calls** with all 128 tools discovered.
- The explicit live test used a model override only in memory. The user's saved Gemini 2.5 Flash selection and other settings were left unchanged.
- No TopSolid mutation occurred. The final native list is in `artifacts/pdm-names/ALL-PROJECTS-AND-LIBRARIES.txt`; model answer and trace are alongside it. These contain local PDM names and are not bundled with the application.

Reproduce the read-only live AI test only when sending project/library names to the configured Gemini service is intended:

```powershell
dotnet run --project TopSolid.Automation.Tests -c Release -- --server .\TopSolid.Automation.Mcp.Server.AddIn\bin\Release\net48\TopSolid.Automation.Mcp.Server.AddIn.exe --live-pdm-names --live-pdm-model gemini-3.6-flash
```

## Running the updated build

Close the older Studio window and open `artifacts/TopSolid-AI/TopSolid.Automation.AI.Studio.exe`. Both Studio and its MCP server are version 0.3.2. Select `gemini-3.6-flash` and save settings. A saved MCP path pointing into another Studio build's `McpServer` folder is resolved to this build's bundled server, so an old Debug path does not retain old tool behavior. Standalone/custom MCP server paths are preserved. The old running process is not terminated by the update.
