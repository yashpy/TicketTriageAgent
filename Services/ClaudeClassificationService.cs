using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TicketTriageAgent.Configuration;

namespace TicketTriageAgent.Services;

/// <summary>
/// Wraps the Anthropic Messages API. Each call is a separate, single-purpose step
/// (classify OR draft) so each step of the agent's workflow is independently
/// loggable, testable, and swappable — this is the "orchestration" part of the
/// project, not just "call an LLM and return text."
/// </summary>
public class ClaudeClassificationService : IClassificationService
{
    private readonly HttpClient _http;
    private readonly ClaudeOptions _options;

    private const string CategoryList =
        "billing, bug_report, feature_request, account_access, general_question";

    public ClaudeClassificationService(HttpClient http, IOptions<ClaudeOptions> options)
    {
        _http = http;
        _options = options.Value;

        _http.BaseAddress ??= new Uri("https://api.anthropic.com/");
        _http.DefaultRequestHeaders.Remove("x-api-key");
        _http.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        _http.DefaultRequestHeaders.Remove("anthropic-version");
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ClassificationOutcome> ClassifyAsync(string subject, string body, CancellationToken ct = default)
    {
        var systemPrompt =
            $"You are a support ticket classifier. Categories: {CategoryList}. " +
            "Respond with ONLY a JSON object, no prose, no markdown fences: " +
            "{\"category\": string, \"confidence\": number between 0 and 1, \"reasoning\": string (max 2 sentences)}.";

        var userPrompt = $"Subject: {subject}\n\nBody: {body}";

        var raw = await CallClaudeAsync(systemPrompt, userPrompt, ct);
        var json = JsonDocument.Parse(ExtractJson(raw));

        return new ClassificationOutcome(
            json.RootElement.GetProperty("category").GetString() ?? "general_question",
            json.RootElement.GetProperty("confidence").GetDouble(),
            json.RootElement.GetProperty("reasoning").GetString() ?? string.Empty);
    }

    public async Task<DraftOutcome> DraftResponseAsync(
        string subject,
        string body,
        string category,
        IReadOnlyList<string> retrievedContext,
        CancellationToken ct = default)
    {
        var contextBlock = retrievedContext.Count > 0
            ? string.Join("\n---\n", retrievedContext)
            : "(no relevant knowledge base articles found)";

        var systemPrompt =
            "You are a support agent drafting a reply to a customer ticket using only the provided " +
            "knowledge base context. If the context does not fully answer the question, lower your confidence. " +
            "Respond with ONLY a JSON object, no prose, no markdown fences: " +
            "{\"draft\": string, \"confidence\": number between 0 and 1}. " +
            "Confidence reflects how well the knowledge base context supports the draft you wrote — " +
            "not how confident you are in general.";

        var userPrompt =
            $"Category: {category}\n\nTicket Subject: {subject}\n\nTicket Body: {body}\n\n" +
            $"Knowledge Base Context:\n{contextBlock}";

        var raw = await CallClaudeAsync(systemPrompt, userPrompt, ct);
        var json = JsonDocument.Parse(ExtractJson(raw));

        return new DraftOutcome(
            json.RootElement.GetProperty("draft").GetString() ?? string.Empty,
            json.RootElement.GetProperty("confidence").GetDouble());
    }

    private async Task<string> CallClaudeAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var payload = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("v1/messages", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Claude API call failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var textBlock = doc.RootElement
            .GetProperty("content")
            .EnumerateArray()
            .First(b => b.GetProperty("type").GetString() == "text");

        return textBlock.GetProperty("text").GetString() ?? "{}";
    }

    /// <summary>
    /// Defensive parsing: strips markdown fences if the model ignores instructions
    /// and wraps its JSON in ```json ... ``` anyway.
    /// </summary>
    private static string ExtractJson(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
            {
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }
        return trimmed;
    }
}
