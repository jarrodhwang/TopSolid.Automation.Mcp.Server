# Live local smoke runner

From the repository root, with Ollama running and a tool-capable model installed:

```powershell
dotnet run --project scripts/LiveWorkflow/LiveSmoke.csproj -c Release -- qwen3.5:9b-q4_K_M
```

Optional positional arguments are the model name, MCP server executable path, and log path.

The runner uses the application's actual `ProviderFactory`, `ChatSession`, and `StdioMcpClient`, and the built bundled server. It asks for TopSolid status and the active document, requires each expected tool call, and records the actual tool results and final model answers in `live-result.txt`. Each complete user turn is limited to three minutes. It does not read or modify saved settings, access cloud credentials, or change provider behavior. If TopSolid is unavailable or no document is active, the returned result must say so.

This is live backend workflow evidence; it does not automate the WPF interface or create/modify a TopSolid document.

