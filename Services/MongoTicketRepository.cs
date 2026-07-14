using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TicketTriageAgent.Configuration;
using TicketTriageAgent.Models;

namespace TicketTriageAgent.Services;

public class MongoTicketRepository : ITicketRepository
{
    private readonly IMongoCollection<Ticket> _collection;

    public MongoTicketRepository(IOptions<MongoOptions> options)
    {
        var opts = options.Value;
        var client = new MongoClient(opts.ConnectionString);
        var db = client.GetDatabase(opts.Database);
        _collection = db.GetCollection<Ticket>(opts.Collection);
    }

    public async Task<Ticket> InsertAsync(Ticket ticket, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(ticket, cancellationToken: ct);
        return ticket;
    }

    public Task<Ticket?> GetAsync(string id, CancellationToken ct = default) =>
        _collection.Find(t => t.Id == id).FirstOrDefaultAsync(ct)!;

    public async Task<List<Ticket>> GetAllAsync(CancellationToken ct = default) =>
        await _collection.Find(FilterDefinition<Ticket>.Empty)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<Ticket>> GetByStatusAsync(TicketStatus status, CancellationToken ct = default) =>
        await _collection.Find(t => t.Status == status)
            .SortBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task ReplaceAsync(Ticket ticket, CancellationToken ct = default)
    {
        ticket.UpdatedAt = DateTime.UtcNow;
        await _collection.ReplaceOneAsync(t => t.Id == ticket.Id, ticket, cancellationToken: ct);
    }
}
