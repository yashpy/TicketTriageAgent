using TicketTriageAgent.Models;

namespace TicketTriageAgent.Services;

public interface IKnowledgeBaseService
{
    Task<List<string>> SearchAsync(string query, string? category, int topK = 3, CancellationToken ct = default);
}
