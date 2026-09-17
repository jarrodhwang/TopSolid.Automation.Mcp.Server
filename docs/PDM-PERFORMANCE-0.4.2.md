# PDM list performance and offline reference — 0.4.2

## Reported failure

The 20260916-013439 export took about 148 seconds to list 50 projects and 66 libraries. Approximately 67 seconds elapsed before the MCP calls and 81 seconds afterward; the two native queries completed within a second. The application supplied 96 schemas and requested a second model response even though it already rendered the returned records itself.

## Change

Simple, unfiltered project/library name requests now send only the requested one or two discovered read-only tool schemas, a short system instruction, and the current user message. The selected model still requests the MCP calls. Studio renders completed receipts immediately without sending all names back for a second inference. Bounded automatic pagination preserves duplicates and explicit incomplete counts. Filters, dates, comparisons, changes and other complex requests retain the normal workflow; the narrow classifier does not silently reinterpret them as a name list.

Name lists no longer connect to PDM Explorer or attach repeated unavailable-date explanations. `includeCreationDates=true` enables the optional date query when requested. Native access remains inside the MCP server, and write confirmation is unchanged.

## Live measurements

Request: `List of all projects and libraries name`. Both tests verified all 50 project and 66 library names against native reads, preserved saved settings and executed no mutations.

| Model | Time | Model requests | MCP calls | Request bytes |
| --- | ---: | ---: | ---: | ---: |
| Gemini 3.6 Flash | 1.865 s | 1 | 2 | 2,186 |
| Ollama gemma4:12b-it-q4_K_M | 40.006 s | 1 | 2 | 2,169 |

Gemini reported 484 prompt tokens and 711 total tokens. Gemma reported 438 prompt tokens, 206 evaluated output tokens and 17.432 seconds of model loading. These are individual measurements on this machine, not latency guarantees under different hardware, network or provider load. Evidence is in `artifacts/pdm-names/*-timing.json`; exact monetary pricing was not estimated.

## Local Automation corpus

The server already used embedded offline references, not website searches during chat. The new `TopSolid.Automation.Mcp.Server.AddIn/TopSolid.Automation` folder contains 6,898 publisher xref symbol entries and 3,335 unique API pages from all eight reference modules. It includes a Markdown index, individual Markdown and JSON articles, exact original HTML, and source SHA-256 hashes. Signatures, descriptions, parameters, returns, remarks and examples are preserved where supplied. Overloads and enum fields may share pages. This is Automation documentation, not the separate ADS/internal API corpus.

Runtime lookup loads one article on demand and caches up to 32 articles. Exact symbols use a dictionary; search uses the loaded symbol index. The embedded snapshot remains the fallback for missing files. No online reference request or arbitrary API execution is introduced. Build/publish bundles the folder alongside MCP.

All local source hashes and symbol mappings passed verification, and the live publisher xref matched the cached index. Read-only protocol checks confirmed local-file responses across every module, taking 4–64 ms including the initial index load; symbol search took about 10 ms.

## Validation and limits

- Release build: zero warnings/errors.
- Application suite: 27/27 groups with the live Gemini test, and 27/27 with the live Ollama test.
- Server: 295 offline checks; protocol: 73 checks.
- Local corpus: every file, hash and mapping verified; eight-module runtime retrieval verified.
- No CAD mutations, automatic model switching or saved-settings changes. Full GUI interaction was not retested.

Rebuild the corpus with `cache_reference.py` and `export_local_reference.py`; verify using `verify_local_reference.py` and `test_local_reference.py` under `scripts/ApiReference`.
