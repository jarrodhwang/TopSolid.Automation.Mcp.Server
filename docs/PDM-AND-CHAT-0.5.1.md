# Sketch defaults, PDM latency and chat — 0.5.1

## What the supplied log showed

`TopSolid-AI-current-log-20260916-121106.json` used Studio 0.5.0 and the **Debug** bundled MCP executable. The sorted project list took about 37 seconds: two model requests received 96 schemas, while the native list query took less than a second. Creating the named part took about 112 seconds and four model rounds before execution. The fifth request hit HTTP 429 after TopSolid had already created the part.

Both Debug and Release outputs are now 0.5.1. Saved provider settings and credentials were preserved.

## 1. Sketch drawings do not create sections by default

- Circle and rectangle tools accept `createSection`, default **false**. They return a profile and `sectionCreated: false` when no section was requested.
- New/appended line-and-arc contours default to `createSection: false`.
- Batch sketch profiles default to `sectionMode: none`; ordinary open polylines no longer require an explicit override.
- Explicit section creation remains available. `createSection: true`, `sectionMode: perProfile/combined`, or the dedicated section tool is appropriate only when requested or needed by a requested modeling operation.
- The combined rectangle-extrusion tool still creates the section required for its solid. Loft workflows use returned profile handles directly.
- Schemas, confirmation defaults, chat guidance and the optional native workflow script agree on these defaults. No geometry was changed during this validation.

## 2. Simple PDM requests use direct MCP dispatch

Recognized full-list requests go directly to their discovered read-only MCP tools. Studio renders the authoritative records and follows continuation offsets. Neither an initial model request nor a final model transcription is required. The reported wording, including `cretaion`, is supported:

```text
List all projects name order by cretaion date (oldest to newest)
list all projects and libraries
oldest first
```

Filters, comparisons, negation and unsupported sorting fields retain the normal model workflow. Alphabetical ordering is not interpreted as creation-date ordering. The direct path preserves ISO date strings and reports missing/invalid sorting data explicitly.

Single quoted creation commands also avoid model inference:

```text
Create a project named "Example"
can u create one part document named "Example part" in project "Example"
```

Part creation reads current project names and loaded-document metadata in batches. It requires one unambiguous working-project match and an extension observed on an actual native `PartDocument`; it does not invent `.TopPrt`. Missing metadata, duplicate targets and partial reads stop the proposal. Complex or unquoted instructions use the general chat workflow.

**Every creation still passes through the existing server preview and explicit user confirmation.** Tokens, exact arguments and target validation are unchanged. Success is rendered from the native receipt, so a provider quota failure cannot hide a successful direct creation. Partial/uncertain outcomes are retained without automatic retry.

General chat now starts with **24 schemas**, with request-relevant sketch/PDM tools prioritized. The existing schema selector can expand the catalog up to the 96-schema limit. Schema selection does not execute or approve anything. Model-request durations are logged separately.

### Measured live results

Measured through `ChatSession` against the same Debug MCP path as the log, with TopSolid and MCP already connected:

| Request | Elapsed | Model requests | Native changes |
|---|---:|---:|---:|
| Complete project list, oldest first | 0.177 s | 0 | 0 |
| All project and library names | 0.019 s | 0 | 0 |
| Named project creation preview, declined | 0.003 s | 0 | 0 |
| Named part lookup and creation preview, declined | 0.075 s | 0 | 0 |

The creation measurements include preparation and an immediate test decline. They do **not** measure execution after approval or human review time. No test object was created. Model inference is bypassed for these precise requests, so Gemini quota and local model loading do not add latency to them. Gemma 4 31B is not installed on this machine; no claim is made that general 31B inference was benchmarked or will always finish within ten seconds.

## 3–4. Elapsed time and blue replies

Chat displays a running elapsed timer and retains elapsed seconds beside each final response/error. Timing includes time spent reviewing a confirmation. The structured export adds `elapsedMilliseconds`, and automatic diagnostics record final chat duration plus individual model-request durations.

The transcript uses read-only rich text so **assistant replies are blue** while user/system entries retain their usual color. Text remains selectable/copyable and bounded in memory. Direct MCP responses are identified as `Model: not used (direct MCP)` instead of claiming a model answered.

## Validation and practical limits

- Debug and Release builds: **zero warnings/errors**.
- **2,816 server checks**: defaults, explicit sections, open contours, solid requirements, schemas, confirmation and existing transaction/identity guards.
- **861 protocol checks** on the configured Debug bundled server; all 163 tools remain registered, including 46 confirmed actions.
- **29/29 application groups** with `--ui-behavior --live-pdm-fast`: provider fixtures, MCP, strict shortcut parsing, no-inference dispatch, pagination, changed/ambiguous targets, confirmation denial, partial receipts, timing export and real WPF chat-control behavior.
- The full off-screen **raster screenshot check still returns a blank bitmap** in this environment. It is kept as a separate failing check; control-level blue/timer validation is not represented as successful visual screenshot validation.
- New sketch defaults and creation execution were not exercised against native geometry/PDM writes. Only live reads and declined creation previews ran.

Quality trade-offs: precise shortcuts provide fast, predictable behavior without a model round, while broad language retains model interpretation. Creation does not trade away confirmation or live identity checks. A small initial tool catalog reduces prompt cost; an unusual capability may require a schema-selection round. Windows/TopSolid prerequisites and the existing provider/MCP separation remain unchanged.

Evidence is under `artifacts/pdm-fast-0.5.1`. The versioned application bundle is `artifacts/TopSolid-AI-0.5.1`; the Debug application used in the log was also rebuilt.
