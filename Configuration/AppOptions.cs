namespace TicketTriageAgent.Configuration;

public class ClaudeOptions
{
    public const string SectionName = "Claude";

    /// <summary>
    /// Prefer setting this via the ANTHROPIC_API_KEY environment variable instead of
    /// appsettings.json. Program.cs overrides this value with the env var if present.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model string — check https://docs.claude.com for the current list before deploying,
    /// model names change over time.
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-4-5-20250929";

    public int MaxTokens { get; set; } = 1024;
}

public class GroqOptions
{
    public const string SectionName = "Groq";

    /// <summary>
    /// Prefer setting this via the GROQ_API_KEY environment variable instead of
    /// appsettings.json. Program.cs overrides this value with the env var if present.
    /// Get a free key at https://console.groq.com/keys.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Check https://console.groq.com/docs/models for the current list — Groq
    /// retires/renames models more often than Anthropic does.
    /// </summary>
    public string Model { get; set; } = "llama-3.3-70b-versatile";

    public int MaxTokens { get; set; } = 1024;
}

public class MongoOptions
{
    public const string SectionName = "Mongo";

    public bool UseMongo { get; set; } = false;
    public string ConnectionString { get; set; } = string.Empty;
    public string Database { get; set; } = "ticket_triage";
    public string Collection { get; set; } = "tickets";
}

public class TriageOptions
{
    public const string SectionName = "Triage";

    /// <summary>
    /// Draft-confidence threshold above which the agent auto-resolves a ticket
    /// instead of routing it to a human for approval.
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.80;
}
