using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinTrans.Services;

public class ClaudeApiClient
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private const string DefaultBaseUrl = "https://api.anthropic.com";
    private const string Model = "claude-sonnet-4-5";
    private const string AnthropicVersion = "2023-06-01";

    public async Task<string> TranslateAsync(string apiKey, string text, string targetLanguage, string style,
        string? baseUrl = null)
    {
        var root = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
        var endpoint = root + "/v1/messages";

        var systemPrompt =
            "You are a professional translator. " +
            $"Translate the user's text into: {targetLanguage}. " +
            $"Use this style: {style}. " +
            "Output ONLY the translation itself — no comments, no quotes, no explanations. " +
            "CRITICAL: preserve the original formatting EXACTLY — every newline, empty line, " +
            "indentation, bullet, numbered list, markdown element and code block must stay " +
            "on the same line it was in the source. Do NOT merge paragraphs, do NOT collapse " +
            "line breaks into spaces, do NOT re-wrap text. If the source has 3 lines, output 3 lines.";

        var payload = new
        {
            model = Model,
            max_tokens = 4096,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = text }
            }
        };

        var json = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);

        using var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Claude API {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            throw new Exception("Unexpected API response: " + body);
        }

        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                block.TryGetProperty("text", out var t))
            {
                sb.Append(t.GetString());
            }
        }
        // Нормализуем окончания строк в Windows-стиль, чтобы приложения вроде
        // Notepad/Word корректно отображали переносы при вставке
        var result = sb.ToString().Trim();
        result = result.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        return result;
    }
}
