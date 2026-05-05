using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        var normalizedSrc = text.Replace("\r\n", "\n").Replace("\r", "\n");
        int lineCount = normalizedSrc.Split('\n').Length;

        var systemPrompt =
            "You are a professional translator. Follow the user's instructions precisely. " +
            "Never add commentary, never apologize, never refuse — always produce the translation.";

        // xml tags are the most reliable way to get claude to preserve formatting
        var userPrompt =
            $"Translate the text inside <source> tags into {targetLanguage}.\n" +
            $"Style: {style}.\n" +
            $"Rules:\n" +
            $"1. Preserve the original formatting EXACTLY. Every newline, empty line, indentation, bullet, markdown element and code block must stay on the same position it was in the source.\n" +
            $"2. The source has {lineCount} line(s). Your translation MUST have {lineCount} line(s). Do NOT merge lines. Do NOT collapse newlines into spaces. Do NOT re-wrap text.\n" +
            $"3. Output ONLY the translation wrapped in <translation> ... </translation> tags. No other text.\n\n" +
            $"<source>\n{normalizedSrc}\n</source>";

        var payload = new
        {
            model = Model,
            max_tokens = 4096,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
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
        var raw = sb.ToString();

        var match = Regex.Match(raw, @"<translation>\s*\n?(.*?)\n?\s*</translation>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var extracted = match.Success ? match.Groups[1].Value : raw;

        // trim leading/trailing blank lines but leave internal ones alone
        extracted = extracted.Trim('\r', '\n', ' ', '\t');

        // normalize to windows line endings
        extracted = extracted.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        return extracted;
    }
}
