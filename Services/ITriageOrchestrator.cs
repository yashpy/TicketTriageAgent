using TicketTriageAgent.Models;

namespace TicketTriageAgent.Services;

public interface ITriageOrchestrator
{
    Task<Ticket> RunAsync(string subject, string body, CancellationToken ct = default);
    Task<Ticket> ApproveAsync(string ticketId, string? note, CancellationToken ct = default);
    Task<Ticket> RejectAsync(string ticketId, string reason, CancellationToken ct = default);
}
