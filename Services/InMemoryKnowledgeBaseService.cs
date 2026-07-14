using TicketTriageAgent.Models;

namespace TicketTriageAgent.Services;

/// <summary>
/// Naive keyword-overlap retrieval over a seeded FAQ set. This stands in for a real
/// vector store (pgvector, Pinecone, etc.) — the orchestrator only depends on
/// IKnowledgeBaseService, so swapping in real embeddings later is a one-file change,
/// not a rewrite of the agent.
/// </summary>
public class InMemoryKnowledgeBaseService : IKnowledgeBaseService
{
    private readonly List<KnowledgeArticle> _articles = new()
    {
        new KnowledgeArticle
        {
            Title = "How to reset your password",
            Content = "Go to Settings > Security > Reset Password. A reset link is emailed within 5 minutes. " +
                      "Links expire after 30 minutes. If no email arrives, check spam or contact support to " +
                      "manually trigger a reset.",
            Tags = new() { "password", "reset", "login", "account_access" }
        },
        new KnowledgeArticle
        {
            Title = "Billing cycle and invoice questions",
            Content = "Invoices are generated on the 1st of each month and charged to the card on file within " +
                      "24 hours. Failed charges retry 3 times over 5 days before the account is downgraded. " +
                      "Refunds for annual plans are prorated; monthly plans are not refundable mid-cycle.",
            Tags = new() { "billing", "invoice", "payment", "refund" }
        },
        new KnowledgeArticle
        {
            Title = "Reporting a bug",
            Content = "Bug reports should include: steps to reproduce, expected vs actual behavior, browser/OS, " +
                      "and a screenshot if visual. Critical bugs (data loss, security) are triaged within 2 hours; " +
                      "others within 2 business days.",
            Tags = new() { "bug", "error", "crash", "issue", "bug_report" }
        },
        new KnowledgeArticle
        {
            Title = "Submitting a feature request",
            Content = "Feature requests are logged to the public roadmap board. We don't commit to timelines on " +
                      "individual requests, but highly-upvoted items are reviewed quarterly by the product team.",
            Tags = new() { "feature", "request", "roadmap", "suggestion", "feature_request" }
        },
        new KnowledgeArticle
        {
            Title = "Account locked or access denied",
            Content = "Accounts lock after 5 failed login attempts within 10 minutes, and auto-unlock after 30 " +
                      "minutes. For SSO-managed accounts, access issues are usually on the identity-provider side " +
                      "— ask the user to check with their IT admin first.",
            Tags = new() { "locked", "access", "denied", "sso", "account_access" }
        }
    };

    public Task<List<string>> SearchAsync(string query, string? category, int topK = 3, CancellationToken ct = default)
    {
        var queryTokens = Tokenize(query);

        var scored = _articles
            .Select(a =>
            {
                var articleTokens = Tokenize(a.Content + " " + a.Title + " " + string.Join(' ', a.Tags));
                var overlap = queryTokens.Intersect(articleTokens).Count();

                // Give a boost if the predicted category matches a tag exactly.
                var categoryBoost = (category is not null && a.Tags.Contains(category)) ? 5 : 0;

                return (Article: a, Score: overlap + categoryBoost);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => $"{x.Article.Title}: {x.Article.Content}")
            .ToList();

        return Task.FromResult(scored);
    }

    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(new[] { ' ', '.', ',', '\n', '\r', '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();
}
