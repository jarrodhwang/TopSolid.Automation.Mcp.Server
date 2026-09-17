using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.AI;

internal sealed class AiHttpClient : IDisposable
{
    private const int MaximumResponseBytes = 8 * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly bool _anthropic;
    private readonly string _authenticationHint;
    internal TimeSpan RequestTimeout { get; }

    public AiHttpClient(Uri baseUri, string? apiKey, HttpMessageHandler? handler = null,
        bool anthropic = false, string authenticationHint = "Check the API key for this endpoint.", TimeSpan? requestTimeout = null)
    {
        RequestTimeout = requestTimeout ?? TimeSpan.FromMinutes(Settings.AppSettings.DefaultRequestTimeoutMinutes);
        if (RequestTimeout <= TimeSpan.Zero || RequestTimeout > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        // Redirects are rejected so neither prompts nor API credentials move to
        // another origin. Local and cloud endpoints use the same policy.
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = baseUri,
            Timeout = Timeout.InfiniteTimeSpan
        };
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        if (_apiKey?.Any(char.IsControl) == true) throw new ArgumentException("The API key contains invalid control characters.");
        _anthropic = anthropic;
        _authenticationHint = authenticationHint;
    }

    public async Task<JObject> SendAsync(HttpMethod method, string path, JObject? body,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (_anthropic)
            {
                request.Headers.Add("anthropic-version", "2023-06-01");
                if (_apiKey is not null) request.Headers.Add("x-api-key", _apiKey);
            }
            else if (_apiKey is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            if (body is not null)
                request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (attempt < 2 && response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
                {
                    var delay = response.Headers.RetryAfter?.Delta ??
                        (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : TimeSpan.FromSeconds((1 << attempt) + Random.Shared.NextDouble() / 4));
                    if (delay <= TimeSpan.FromSeconds(10))
                    {
                        await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, timeout.Token).ConfigureAwait(false);
                        continue;
                    }
                }
                var detail = response.StatusCode == HttpStatusCode.NotFound
                    ? await NotFoundDetail(response, timeout.Token).ConfigureAwait(false) : "";
                throw new AiProviderException(DescribeHttpError(response.StatusCode) + detail +
                    (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? " " + _authenticationHint : ""));
            }
            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
                throw new AiProviderException("The AI endpoint returned a response larger than 8 MB.");

            using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) != 0)
            {
                if (output.Length + read > MaximumResponseBytes)
                    throw new AiProviderException("The AI endpoint returned a response larger than 8 MB.");
                await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
            }
            output.Position = 0;
            using var textReader = new StreamReader(output, Encoding.UTF8);
            using var jsonReader = new JsonTextReader(textReader)
            {
                MaxDepth = 64,
                DateParseHandling = DateParseHandling.None
            };
            var result = JObject.Load(jsonReader, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (jsonReader.Read())
                throw new AiProviderException("The AI endpoint returned more than one JSON response. Non-streaming JSON is required.");
            if (result["error"] is { Type: not JTokenType.Null })
                throw new AiProviderException("The AI endpoint reported an error. Check the model, credentials, and server configuration.");
            return result;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException($"The AI request timed out after {RequestTimeout.TotalMinutes:0.##} minutes. Increase AI wait in Configuration or check the model server. Completed tool actions were not retried.");
        }
        catch (HttpRequestException)
        {
            throw new AiProviderException("Could not reach the AI endpoint. Check the URL, network connection, TLS certificate, and server availability.");
        }
        catch (JsonException)
        {
            throw new AiProviderException("The AI endpoint returned invalid JSON. Check that the base URL points to the correct API.");
        }
    }

    private static string DescribeHttpError(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => "AI authentication failed (HTTP 401).",
        HttpStatusCode.Forbidden => "The AI endpoint denied access (HTTP 403). Check the key, account, and model permissions.",
        HttpStatusCode.NotFound => "The AI endpoint or model was not found (HTTP 404). Check the server URL and selected model.",
        HttpStatusCode.TooManyRequests => "The AI endpoint rejected the request due to quota or rate limits (HTTP 429). Check the account or wait and try again.",
        HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
            $"The AI endpoint rejected the request (HTTP {(int)code}). Check that this model supports chat and function tools on the configured API.",
        _ when (int)code >= 300 && (int)code < 400 => "The AI endpoint returned a redirect. Configure its final API base URL; redirects are disabled.",
        _ when (int)code >= 500 => $"The AI server failed to handle the request (HTTP {(int)code}). Check its status and try again.",
        _ => $"The AI endpoint returned HTTP {(int)code}. Check its configuration."
    };

    private async Task<string> NotFoundDetail(HttpResponseMessage response, CancellationToken token)
    {
        // Only an error message from a bounded JSON body is shown, never headers,
        // HTML or request payloads. Redact the credential before truncation.
        using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        var bytes = new byte[8193];
        var length = 0;
        while (length < bytes.Length)
        {
            var count = await stream.ReadAsync(bytes.AsMemory(length), token).ConfigureAwait(false);
            if (count == 0) break;
            length += count;
        }
        if (length >= bytes.Length) return "";
        try
        {
            var root = JToken.Parse(Encoding.UTF8.GetString(bytes, 0, length));
            if (root is JArray array) root = array.First ?? new JObject();
            var value = root is JObject errorObject && errorObject["error"] is JObject error ? error["message"] : null;
            if (value?.Type != JTokenType.String) return "";
            var message = value.Value<string>() ?? "";
            if (_apiKey != null)
            {
                message = message.Replace(_apiKey, "[redacted]", StringComparison.Ordinal);
                message = message.Replace(Uri.EscapeDataString(_apiKey), "[redacted]", StringComparison.Ordinal);
            }
            message = string.Concat(message.Select(c => char.IsControl(c) ? ' ' : c));
            return message.Length == 0 ? "" : " Provider detail: " + message[..Math.Min(800, message.Length)];
        }
        catch (JsonException) { return ""; }
        catch (InvalidOperationException) { return ""; }
    }

    public void Dispose() => _http.Dispose();
}
