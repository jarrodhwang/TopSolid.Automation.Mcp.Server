# AI Studio interface

The main window combines a modern chat workspace with TopSolid's native visual identity. The rounded composer, compact permission and model controls, neutral reply text, and narrow reading width draw on Codex, ChatGPT, Claude, and Gemini. The title and toolbar gradients, original TopSolid icons and native selection colors keep the surrounding controls familiar to a TopSolid operator. Light controls use white edits, black text, lavender-gray panels and orange command feedback; dark controls use blue hover and pressed feedback.

Settings opens from the small icon at the bottom left. Its left menu separates AI Model, Appearance, Languages and Developer pages. Save settings persists the preferences together. The model selector in the composer and Settings edit the same model. Model discovery remains distinct from successful inference. The window retains native caption dragging, resizing, maximize and restore.

The main toolbar uses the three supplied TopSolid refresh, refresh-warning and red error icons unchanged. Startup automatically connects MCP, connects to an already-running TopSolid, checks the selected provider's model list and refreshes the followed theme. It does not launch TopSolid or send a paid chat request. Failed services are reported independently; the draft, attachments, conversation and selected model are preserved. Manual MCP connect/disconnect and Check TopSolid controls live only in the developer dashboard.

The normal icon rotates continuously during checks and stops when ready. Slow checks become warnings after eight seconds. Temporary host startup/nonresponse, unavailable AI service, pending model requests and rate/quota limits use the warning icon; missing TopSolid installation, missing model configuration, rejected credentials and MCP connection failure use the red error icon. Crucial errors take precedence. TopSolid detection is best-effort and read-only; a successful live MCP status overrides local installation detection. Checks have separate deadlines (MCP 20 seconds, TopSolid 25 seconds, AI discovery 20 seconds), with at most three attempts to connect to a running TopSolid. Actual AI request timing is monitored separately; successful model listing cannot clear an earlier inference-quota warning.

Clicking a warning/error opens a themed, resizable English/Korean status dialog with friendly explanations, Cancel and Refresh. Cancel dismisses the dialog. Refresh retries all checks, and is disabled during an active request or CAD mutation. Raw exceptions, paths and identifiers remain in diagnostics rather than the status dialog. Model listing can establish catalogue availability, but cannot prove inference quota or successful generation.

## Appearance

Appearance offers Dark, Light, System and TopSolid. Dark/Light are fixed Studio palettes; System follows the Windows app preference. TopSolid remains the default and follows the saved TopSolid theme, reading `CurrentTheme` from the newest installed user configuration under `%APPDATA%\TOPSOLID\TopSolid\<version>\TopSolid\Kernel\SX\Config.ConfigData.xml`. Missing selection means TopSolid Classic. Custom theme colors are read from that version's `Themes` directory. Following is read-only, checked every two seconds, and retains the last valid theme during a temporary failure. A pending read cannot overwrite a newer manual choice.

The supplied light/dark color tables define the fallback palettes. TopSolid's [Themes documentation](https://help.topsolid.com/7.19/en/TopSolid%27Design/TopSolid/Kernel/WX/Options/themesapplicationoptionscontrol.htm) describes Classic, Dark and custom themes. Asset provenance is kept with the copied icon files in `TopSolid.Automation.AI.Studio/Assets/TopSolid`.

## Chat and permissions

Studio language offers System default, English, Korean, French, Japanese, Spanish and Portuguese. Menus, settings, permissions, approval UI, transcript role labels, diagnostics headings, GPU labels and Studio-owned feedback update immediately; untranslated Studio-owned strings use the English source text until their locale text is added. Unsupported system languages fall back to English. Model IDs, project names, raw tool/provider results and existing message contents retain their original text.

AI response language is independent: Match my message, English, Korean, Japanese, Chinese, French, German, Spanish or Portuguese. A fixed language mapping supplies a trusted system instruction for new model turns. The choice is held stable across tool calls within an active turn; it never interprets attachment instructions as language settings or authorization. Direct MCP receipts retain their source content.

Enter or Ctrl+Enter sends; Shift+Enter inserts a line break. The send button becomes Cancel while work runs. Cancellation remains disabled during a CAD commit/rollback, preserving the existing mutation contract. A failed message restores its text and selected attachments.

An activity strip above the composer shows localized thinking, TopSolid/parameter work, response preparation, approval wait, applying changes and cancellation phases. Indeterminate progress stops while a human decision is required, and completion, cancellation or failure removes the strip. Phase changes have a polite accessibility announcement; tool arguments and internal identifiers never appear in this surface.

User-mode approvals render immutable server facts as effect, target and requested-change cards. Nested parameters and collections remain readable as labeled values, including current/requested values and units when supplied by the server. Operation and parameter icons are copied unchanged from the supplied TopSolid 7.19 icon collection. Approval applies to the entire prepared change; there are no selection controls that could imply unsupported partial execution. Cancel retains the default keyboard action. Raw proposal inspection remains in Dev Mode, and display formatting never changes the proposal used for execution.

The scalar CAM-parameter approval uses the actual prepared `target` fields to foreground the operation, parameter, current native formatted value and proposed scalar. Proposed real numbers are explicitly labeled SI, using a known SI symbol or the exact unit type; the native display `UnitSymbol` is never attached to the proposed SI number. Native enumeration labels and empty text remain meaningful. Replacing a formula/reference and additional synchronized documents are shown before approval. Full friendly native metadata and arguments remain read-only in a collapsed details section, loaded only when expanded.

The permission chip applies a local deterministic policy to prepared MCP proposals:

| Mode | Behavior |
| --- | --- |
| Ask for approval | Review every proposed change. |
| Approve for me | Automatically allow recognized reversible CAD edits. Ask for persistence, deletion, replacement, CAM execution, and unfamiliar actions. |
| Full access | Automatically allow known prepared actions, including persistent or destructive ones. Unfamiliar actions still require review. |

Permission mode starts with Ask for approval for each application session. These modes do not bypass MCP schema validation, target resolution, preparation tokens, transaction handling, or known-result reporting. Project creation remains a persistent PDM action under Approve for me. Approval windows show the relevant TopSolid project/document/sketch/action icon and readable targets and arguments. The immutable full-JSON tab is available in Dev Mode. Cancel is the default action.

The plus button or file drag-and-drop adds up to six text/source files or PNG/JPEG images. A drop overlay identifies the destination. Dropping files only adds them to the draft; it does not send the message or move the source files. Both paths use the same bounded loader and add a batch atomically, so a failed read leaves existing attachments intact. Chips identify and remove each attachment.

Text is limited to 12,000 characters per file and 24,000 total; images to 5 MB each, 12 MB total, and 20 megapixels. PDF, Office and native CAD binaries are not supported. Images are sent in each provider's actual image format; the selected model must support vision. Files go to the selected AI provider when the message is sent. Attachment contents are reference data, separate from typed user intent, and never enter deterministic command parsing.

## Developer dashboard

User mode presents friendly names in responses and approval reviews, with no PDM object IDs, document IDs or GUIDs. Names come from existing server receipts; an unavailable name is explicitly marked instead of guessed. Receipt JSON becomes readable labeled facts, retaining dimensions, warnings and partial failures. Turning Dev Mode on restores the original response details; turning it off reformats existing messages immediately without changing the draft.

This is a presentation boundary, not a change to execution or diagnostic records. Exact identifiers remain in MCP arguments, immutable prepared proposals, model tool context and credential-redacted diagnostic logs. The name cache is bounded to 4,096 entries and adds no server round trips. Document-local entity handles and parent project/document identities are kept distinct. Clearing the conversation clears the name cache. The model also receives a concise user-mode output instruction, but display filtering does not rely on model compliance.

Dev Mode opens a separate resizable window with measured latest-turn elapsed/model/tool/approval durations, formatted events and results, chat/model responses, and a searchable MCP tool catalogue. Missing measurements are shown as unavailable. The dashboard displays at most 400 entries per list and refreshes without rebuilding unchanged details. Save log retains the existing structured diagnostic export; API keys and image payloads are excluded. Durable error logging remains active regardless of Dev Mode. Save settings persists the Dev Mode preference.

The GPU usage tab shows actual local Windows WDDM counters: busiest engine per adapter, dedicated/shared memory and a bounded 60-sample history. It does not infer cloud or remote-server GPU use. Sampling runs on a background worker every two seconds only while the tab is visible and the window is not minimized; updates are coalesced and handles are disposed on worker completion. Missing counters display unavailable. Adapter memory counters avoid counting shared allocations once per process. This uses native PDH without an extra runtime package or elevation; see [PDH array handling](https://learn.microsoft.com/en-us/windows/win32/api/pdh/nf-pdh-pdhgetformattedcounterarrayw) and [Microsoft GPU utilization semantics](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/).

The implementation preserves bounded chat/history/diagnostic retention. Controls remain accessible by keyboard and have descriptive tooltips and automation names. UI rendering tests use synthetic data and do not imply successful live AI inference or CAD modification.

## Validation — 2026-09-17

Response presentation update (`UiResponses`):

- Build: 0 warnings, 0 errors. Regression suite with `--ui-behavior --server <UiResponses bundled MCP server>`: 36/36 passed, including real MCP initialization/status and read-only PDM chat.
- Separate `--ui-shell --no-ui-render`: passed mode switching, queued message deduplication, unchanged draft, raw diagnostic retention, user/developer approval contents and immutable proposal checks, plus the existing theme/settings/GPU behavior checks. Saved settings remained unchanged.
- Tests cover GUID formats, Markdown destinations, embedded JSON, failed and partial receipts, compound entity handles, duplicate friendly names, parent identity separation, unknown names and unchanged model/tool history.
- Visual validation is pending for this build: both WPF captures and native Computer Use capture were unavailable in the current display session. WPF produced empty images even with the documented process-level [software rendering preference](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.processrendermode?view=windowsdesktop-10.0); native capture failed twice with `IGraphicsCaptureItemInterop.CreateForMonitor` (`0x80070057`). Empty fixture captures now fail explicitly. The `--no-ui-render` option skips only image generation, not behavior assertions; it must not be reported as visual verification.
- Published output: `artifacts/TopSolid-AI-Responses`. No live AI inference or CAD mutation was needed for this response-display change.

Earlier appearance/settings validation (`UiSettings`):

- `UiSettings` build of `TopSolid.Automation.Tests.csproj`: 0 warnings, 0 errors.
- Regression suite with `--ui-smoke --server <UiSettings bundled MCP server>`: 35/35 passed. Includes real MCP initialization/discovery/status and direct read-only PDM chat through the developer controls, provider wire-format fixtures, independent UI/response languages, permission policy, cancellation, GPU reduction/lifecycle checks, settings and diagnostic redaction.
- Separate `--ui-shell`: passed both palettes, chat at 1040×800 and 760×600, every Settings section at full/minimum size, Korean label switching, separate Japanese response preference, model selection preservation, refresh failure recovery with draft/attachments intact, Dev Mode controls, GPU tab and approval rendering. All four appearance choices reached the follower. Saved user settings hashes were unchanged.
- Screenshots: `artifacts/ui-redesign/`. Chat, approval and event examples are synthetic. GPU screenshots contain actual local Windows counter readings; the production sampler also passed a standalone host check. Both palettes, Korean labels, minimum sizing, refreshed toolbar and GPU layout were visually reviewed.
- Published to `artifacts/TopSolid-AI-Settings`; binaries match the validated build. Computer Use checked the native section navigation and immediate light-to-dark switch. Appearance selector alignment was corrected and the final UI fixture rerun. No Windows or TopSolid preference was modified. External Explorer file dragging was not exercised; the shared attachment loader was verified by the UI fixture.
- No paid/live AI inference or CAD mutation was performed during this UI validation. Text and image request serialization was exercised through HTTP fixtures; actual model vision support depends on the selected model.
