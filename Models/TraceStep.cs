namespace TicketTriageAgent.Models;

/// <summary>
/// One logged step in the agent's multi-step workflow (classify, retrieve, draft, decide).
/// This is what makes the agent's reasoning auditable instead of a black box.
/// </summary>
public class TraceStep
{
    public string StepName { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long DurationMs { get; set; }
}
