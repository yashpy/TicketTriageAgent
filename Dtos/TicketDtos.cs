using TicketTriageAgent.Models;

namespace TicketTriageAgent.Dtos;

public record CreateTicketRequest(string Subject, string Body);

public record ApprovalDecisionRequest(string? Note);

public record RejectionDecisionRequest(string Reason);

public record TicketResponse(
    string Id,
    string Subject,
    string Body,
    TicketStatus Status,
    string? Category,
    double? ClassificationConfidence,
    double? DraftConfidence,
    string? DraftResponse,
    List<string> RetrievedContext,
    string? DecisionReason,
    string? RejectionReason,
    List<TraceStep> ReasoningTrail,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static TicketResponse FromTicket(Ticket t) => new(
        t.Id, t.Subject, t.Body, t.Status, t.Category,
        t.ClassificationConfidence, t.DraftConfidence, t.DraftResponse,
        t.RetrievedContext, t.DecisionReason, t.RejectionReason,
        t.ReasoningTrail, t.CreatedAt, t.UpdatedAt);
}

public record EvalStats(
    int TotalTickets,
    int AutoResolved,
    int PendingApproval,
    int Approved,
    int Rejected,
    double AverageClassificationConfidence,
    double AverageDraftConfidence,
    double AutoResolveRate);
