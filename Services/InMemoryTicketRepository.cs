using System.Collections.Concurrent;
using TicketTriageAgent.Models;

namespace TicketTriageAgent.Services;

/// <summary>
/// Default repository so the project runs with zero external dependencies out of the
/// box. Swap to MongoTicketRepository via config once you want persistence across restarts.
/// </summary>
public class InMemoryTicketRepository : ITicketRepository
{
    private readonly ConcurrentDictionary<string, Ticket> _store = new();

    public Task<Ticket> InsertAsync(Ticket ticket, CancellationToken ct = default)
    {
        _store[ticket.Id] = ticket;
        return Task.FromResult(ticket);
    }

    public Task<Ticket?> GetAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<List<Ticket>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult(_store.Values.OrderByDescending(t => t.CreatedAt).ToList());

    public Task<List<Ticket>> GetByStatusAsync(TicketStatus status, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.Where(t => t.Status == status).OrderBy(t => t.CreatedAt).ToList());

    public Task ReplaceAsync(Ticket ticket, CancellationToken ct = default)
    {
        ticket.UpdatedAt = DateTime.UtcNow;
        _store[ticket.Id] = ticket;
        return Task.CompletedTask;
    }
}
