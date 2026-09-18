# Regression harness

Run on Windows with .NET 10:

```powershell
dotnet run --project TopSolid.Automation.Tests
```

The default checks exercise the real OpenAI-compatible, Anthropic Messages and Ollama HTTP adapters
with an in-memory HTTP handler, the chat/MCP dispatch loop with a fake MCP client,
failure and cancellation handling, and Windows DPAPI settings persistence. They do
not contact an AI service or prove that a particular model supports tool calling.
All key values are synthetic test data.

License startup fixtures cover fail-closed decisions, cancellation, malformed protocol data and native metadata semantics. The separate `--ui-shell` suite renders the shared license dialog. A read-only live license probe is available with `--live-licenses <MCP-server.exe>`; it prints validity and field presence, omitting user/owner data. It never changes or deactivates a license.

Cloud checks cover preset routing, Gemini thought-signature replay, Anthropic authentication/pagination/parallel tool results, encrypted per-service profiles, and migration of the old native Gemini URL. The WPF test also checks preset selection, automatic URLs and separate keys/models.

To verify model discovery with the existing saved Gemini key, without inference or CAD data:

```powershell
dotnet run --project TopSolid.Automation.Tests -c Release -- --live-model-list gemini
```

This opt-in check requires the saved service to match the argument, permits only its official preset URL, retrieves model IDs and leaves settings unchanged. It never prints the key. Successful listing is not proof of inference/quota/tool support.

To additionally start the built MCP executable and verify the real stdio
initialize/discovery/status/error/disconnect sequence:

```powershell
dotnet run --project TopSolid.Automation.Tests -- --server "C:\path\to\TopSolid.Automation.Mcp.Server.AddIn.exe"
```

This optional check invokes read-only status. An unavailable TopSolid installation
is a valid status result; success does not establish authenticated AI inference or
live document access in TopSolid. It also runs both real HTTP provider adapters
against simulated AI replies, dispatches their tool calls through the real MCP
process, and checks that the actual status appears in each provider's next request.
The test never creates or modifies CAD data.

Add `--ui-smoke` to construct the actual WPF window on an STA dispatcher, exercise
provider selection and the Connect/Check TopSolid/Disconnect controls, and render
the window's own visual tree to `artifacts/ui-smoke.png`. The test uses synthetic
provider display values, never saves settings, and verifies the settings file is
unchanged. It does not capture the desktop or contact an AI endpoint.

Explicitly opt into **one live user chat turn** with a locally installed,
tool-capable Ollama model:

```powershell
dotnet run --project TopSolid.Automation.Tests -c Release -- --server "C:\path\to\TopSolid.Automation.Mcp.Server.AddIn.exe" --ui-smoke --live-ui-ollama "your-model-name"
```

This additionally sends the TopSolid connection question through the actual WPF
Send control, waits up to five minutes for the model/tool/final-answer loop,
verifies the visible tool call and assistant answer, and includes the answer in
the rendered UI artifact. Ollama must already run on `http://localhost:11434`.
The live option is never enabled by default and still does not save settings.

## Modeling and transport regression tests

The default suite also checks approval/denial, invalid previews, no automatic repeat after denial, and retention of confirmed change receipts when a model follow-up fails. A separate fixture process tests cancellation/disconnect and malformed stdout during a simulated modification. It never connects to TopSolid.

The UI smoke checks exercise both buttons of the actual confirmation dialog with synthetic geometry and render `artifacts/confirmation-smoke.png`. No real CAD modification occurs.

The sibling `TopSolid.Automation.Mcp.Server.Tests` project targets .NET Framework 4.8 and tests server schemas, one-time confirmation tickets, transaction callbacks, geometry conversions and native value serialization without connecting to TopSolid. Build the solution, then run its executable from `bin/Release/net48`.

## Explicit native integration fixture

`scripts/LiveWorkflow/fixture-plan.json` defines the only opt-in native test scope. The Python runner performs read-only preflight by default. After a human explicitly approves the exact plan, `--approve-plan <SHA256>` permits controlled native operations. Keep the receipts; never automatically repeat a failed run.

After the native fixture exists, `--live-ui-ollama <model> --approved-native-plan <SHA256>` exercises the actual Studio Send button, selected model, MCP, real confirmation dialog, and native circle creation. It only clicks Apply for the exact approved fixture document/circle, once, and rejects other changes or synchronized documents. Default tests and ordinary live status tests never enable this mode.
