# Cloud service presets — Studio 0.3.2

Choose **Cloud API → Cloud service**. The service URL fills automatically. Enter that service's API key, click **List models**, choose a chat model with function-tool support, and save settings. **Custom OpenAI-compatible** retains an editable URL; **Ollama** retains its separate local/LAN configuration.

| Service | Preset API base URL | Adapter / source |
|---|---|---|
| OpenAI | `https://api.openai.com/v1` | [OpenAI Chat Completions / models](https://developers.openai.com/api/reference/resources/models/methods/list) |
| Google Gemini | `https://generativelanguage.googleapis.com/v1beta/openai` | [Google's OpenAI compatibility API](https://ai.google.dev/gemini-api/docs/openai) |
| Anthropic (Claude) | `https://api.anthropic.com/v1` | Native [Messages](https://platform.claude.com/docs/en/api/messages/create) and [Models](https://platform.claude.com/docs/en/api/models/list); x-api-key and anthropic-version headers |
| xAI (Grok) | `https://api.x.ai/v1` | [OpenAI-compatible inference API](https://docs.x.ai/developers/rest-api-reference/inference) |
| Meta Model API | `https://api.meta.ai/v1` | [Official Meta cookbook](https://github.com/meta-models/meta-model-cookbook); OpenAI-compatible, Meta Model API key |
| Groq / Meta Llama | `https://api.groq.com/openai/v1` | [Groq compatibility API](https://console.groq.com/docs/openai); requires a Groq key |
| Mistral | `https://api.mistral.ai/v1` | [Mistral model API](https://docs.mistral.ai/api/endpoint/models) |
| DeepSeek | `https://api.deepseek.com/v1` | [DeepSeek compatibility API](https://api-docs.deepseek.com/) |
| OpenRouter | `https://openrouter.ai/api/v1` | [OpenRouter API](https://openrouter.ai/docs/quickstart) |

Model IDs come from each endpoint rather than a fixed list. Discovery does not certify that every listed model supports chat or MCP function calls. Listing opens the dropdown but does not automatically select an arbitrary first model. A manually entered model ID remains supported.

## Gemini 401 correction

The reported configuration used Google's native `/v1beta` base URL with the OpenAI-compatible adapter. The adapter needs `/v1beta/openai`; otherwise authentication, model-list shape and chat routing do not match the native API.

Loading that exact legacy Google HTTPS URL now selects Gemini, changes only the API path, preserves the already decrypted Google key, and removes an optional `models/` prefix from the saved model ID. **Save settings** persists the migrated endpoint and re-encrypts the key for its new endpoint scope. Unrelated/custom URLs are not rewritten. An actual read-only request with the existing saved key returned **58 model IDs** after this correction on 2026-09-15. No chat, CAD information or paid inference was sent.

Gemini function-call and assistant `extra_content` are replayed unchanged so thought signatures survive the MCP round trip, following [Google's signature requirements](https://ai.google.dev/gemini-api/docs/generate-content/thought-signatures). OpenAI-compatible reasoning_content is also retained for services that require it.

## Credential and provider boundaries

- Each cloud service keeps its own endpoint, key and model. Switching service restores that profile instead of forwarding the previous service's key.
- Keys use Windows DPAPI CurrentUser and endpoint-bound entropy, including inactive profiles. Plain JSON serialization excludes keys. Endpoint changes invalidate credentials. Named presets reject mismatched URLs before sending requests.
- Existing version-1 settings remain readable. New optional profile fields preserve active-root compatibility; old application versions will not preserve inactive profiles when saving.
- Anthropic uses a separate Messages adapter. System prompts are top-level; parallel tool results share one user turn; content/thinking signatures replay intact; incomplete output cannot dispatch tools. Model pagination is bounded to 20 pages.
- API errors remain sanitized. Authentication errors explain which service's key is needed; redirects are disabled. Model listing status remains separate from inference status.

## Validation

25 application test groups passed with real stdio, WPF controls and the opt-in live Gemini list. Coverage includes nine cloud routes, Gemini signature round trips, Anthropic tool calls/pagination/incomplete-output rejection, profile isolation, DPAPI persistence, legacy Gemini migration, and unchanged user settings. Evidence: `artifacts/cloud-provider-tests.txt`.

The 0.3.1 update authenticated Gemini model discovery only. In 0.3.2, a real Gemini 3.6 Flash conversation retrieved all 116 PDM project/library names in two MCP calls; see [PDM validation](PDM_NAME_LISTS.md). Gemini 2.5 Flash remains listed but Google's response rejects it for this account. Other cloud routes used synthetic HTTP responses. Native CAD execution remains subject to explicit user approval.
