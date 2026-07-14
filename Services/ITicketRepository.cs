using TicketTriageAgent.Models;

namespace TicketTriageAgent.Services;

public interface ITicketRepository
{
    Task<Ticket> InsertAsync(Ticket ticket, CancellationToken ct = default);
    Task<Ticket?> GetAsync(string id, CancellationToken ct = default);
    Task<List<Ticket>> GetAllAsync(CancellationToken ct = default);
    Task<List<Ticket>> GetByStatusAsync(TicketStatus status, CancellationToken ct = default);
    Task ReplaceAsync(Ticket ticket, CancellationToken ct = default);
}
