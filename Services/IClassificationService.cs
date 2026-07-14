namespace TicketTriageAgent.Services;

public record ClassificationOutcome(string Category, double Confidence, string Reasoning);

public record DraftOutcome(string DraftText, double Confidence);

public interface IClassificationService
{
    Task<ClassificationOutcome> ClassifyAsync(string subject, string body, CancellationToken ct = default);

    Task<DraftOutcome> DraftResponseAsync(
        string subject,
        string body,
        string category,
        IReadOnlyList<string> retrievedContext,
        CancellationToken ct = default);
}
