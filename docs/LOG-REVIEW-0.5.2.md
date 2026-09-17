# Request waiting, PDM speed and creation access — 0.5.2

## What failed in the 13:00 log

The supplied `TopSolid-AI-current-log-20260916-130050.json` used Studio 0.5.1, the Debug bundled MCP executable and local Ollama `gpt-oss:20b`.

- The alphabetical request missed the strict direct-list parser. Three model requests took 42.18, 7.55 and 130.39 seconds. The two native list calls returned **51 projects and 66 libraries**, but the model wrote inaccurate lists/counts (46 and 70) and changed names.
- Document creation existed in MCP. The initial 24 schemas omitted it when the request combined PDM and sketch work. The model treated that subset as the entire catalog.
- A later discovery round sent the full capability response and old inventory history back to the model, then hit the fixed three-minute HTTP timeout.

## Changes

### Model waiting and local inference

**AI wait** defaults to **15 minutes per model HTTP request**, configurable from **1 to 60 minutes** in Configuration. It applies to Ollama, OpenAI-compatible services and Anthropic, persists with settings and appears in diagnostic exports. Existing settings receive the new default without rewriting credentials. Cancel remains available; a timeout does not automatically repeat a request or a native action.

**Faster GPT-OSS** uses Ollama's documented `think: "low"` for the exact GPT-OSS model family. Unchecking it restores the model default; other model families receive no new thinking parameter. This trades reasoning depth for latency and is optional. Source: [Ollama thinking controls](https://docs.ollama.com/capabilities/thinking).

### PDM lists and ordering

These requests use live MCP results directly, with **zero model calls**:

```text
List all projects and libraries name list order by alphabetically
list projects order by name ascending
list projects sorted A-Z
list all projects reverse alphabetically
reverse alphabetical order
List all projects name order by cretaion date (oldest to newest)
```

The existing `topsolid_list_projects` and `topsolid_list_libraries` tools now support `orderBy: nameAscending/nameDescending` as well as chronological order. Sorting happens across the full collection before pagination. Friendly names are preserved exactly, duplicate names retain their separate records, and names are never transcribed by the model. Name comparison is invariant-culture, case-insensitive, with PDM ID as a deterministic tie-breaker. Alphabetical ordering does not request creation dates unless the user also asks for them.

Prior full inventory turns remain in the visible transcript and diagnostics but are reduced to a short reminder for later model requests. Live lookups still query current data. Filters and complex instructions retain the general model workflow instead of silently returning an unrelated full inventory.

### Creation tools and access

The catalog contains **165 tools: 118 reads/reference tools and 47 confirmed actions**.

- **`topsolid_get_document_creation_context`** resolves all working-project matches for an exact friendly name and groups observed document types/extensions from loaded documents in one MCP call. Duplicate names, incomplete reads and missing types are explicit.
- **`topsolid_create_part_document`** accepts only `ownerId` and `name`. The server verifies one extension from actual loaded native `PartDocument` objects and uses TopSolid's default template. This prevents repeated model attempts to supply conflicting template/extension arguments. The preview includes the resolved extension, and the server rechecks the target before execution. A missing or ambiguous observed part type blocks creation; a specifically requested template uses the existing generic document-creation tool.

These tools use compiled public kernel Automation calls: `IPdm.GetProjects/GetName/GetType/Exists/CreateDocument/SetName` and `IDocuments.GetDocuments/GetTypeFullName/GetPdmObject/GetDocument`. Their 7.20 contracts are in the bundled local reference; no undocumented function or guessed extension was added.

Initial schema selection now includes document creation/open/save and the requested sketch tools together. A compact catalog of **all registered names** accompanies the initial subset. The model can select omitted schemas directly without querying the native capability catalog first. A call to a discovered but unexposed tool loads its schema for the next request and executes nothing on that first attempt. The initial limit remains 24 schemas, expandable to 96.

Creation still requires the existing preview and explicit confirmation. Declining now ends the turn immediately, records skipped calls and avoids another model request or a repeated confirmation question. Persistent/uncertain creation receipts and mutation-aware shutdown are preserved. Sketch drawings still default to **no sections**; the blue reply text and elapsed timer remain.

## Validation

Connected-session timings, excluding startup and human review:

| Request | Observed time | Model requests | Native writes |
|---|---:|---:|---:|
| Exact alphabetical projects + libraries request | 0.23 s | 0 | 0 |
| Reverse alphabetical follow-up | 0.02 s | 0 | 0 |
| Oldest-first projects | 0.09 s | 0 | 0 |
| Unsorted projects + libraries | 0.03 s | 0 | 0 |
| Explicit named-part lookup and declined preview | 0.09 s | 0 | 0 |
| Exact compound part + circle request, local GPT-OSS 20B, through declined part preview | 16.8 s | 2 | 0 |

The intermediate generic creation path took 63.6 seconds and needed two argument-repair rounds. The focused part tool removed those rounds. Earlier Debug measurements were 0.18 seconds for the alphabetical list and 18.6 seconds for the local-model preview; the table records the final Release run. These figures are observations on this connected installation, not guarantees for another model, cold startup or actual geometry execution. No Gemma 31B benchmark or native creation/sketch execution was performed.

Regression coverage includes complete paged inventories and duplicate Korean names, creation schema access for all three reported prompts, partial/ambiguous lookups, observed extensions, timeout defaults/configuration/cancellation, optional thinking control, confirmation denial, partial receipts, credential redaction, WPF controls, blue text and elapsed time. WPF behavior testing does not claim a successful raster screenshot; the existing off-screen screenshot limitation remains.

Final builds have zero warnings/errors. **2,872 server checks**, **876 protocol checks** against the updated Debug executable, and **31/31 Release application groups** passed. All **250 declared source pages** resolve in the local reference. Saved user settings remained unchanged.

Evidence is in `artifacts/log-review-0.5.2`: build logs, server/protocol/application results, `live-timing.json`, and `live-model-access.json`. Both Debug (the path in the user's log) and Release are rebuilt. The framework-dependent bundle is `artifacts/TopSolid-AI-0.5.2`; retain its `McpServer` folder and local API reference.

## Practical quality trade-offs

Direct lists improve both speed and accuracy. The compact tool catalog adds a small prompt cost but avoids native discovery rounds and false tool-availability claims. Exact target matching and observed part types stop ambiguous creation. The longer configurable HTTP deadline accommodates slow local models without weakening native confirmation, bounded output or cancellation. TopSolid access remains entirely inside the Windows MCP server, with provider communication and settings separate in Studio.
