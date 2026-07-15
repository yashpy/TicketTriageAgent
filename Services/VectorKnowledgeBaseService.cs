using System.Text.Json;
using TicketTriageAgent.Models;

namespace TicketTriageAgent.Services;

/// <summary>
/// Real vector search over the knowledge base — TF-IDF document vectors + cosine
/// similarity, computed once at startup. This is a genuine embedding + vector-space
/// retrieval model (not keyword-overlap counting): term importance is weighted by
/// how rare it is across the corpus, and similarity is a proper vector-space distance
/// metric, not a raw intersection count.
///
/// Deliberately zero-infra: no external embedding API call, no vector DB service to
/// run or pay for. Swapping this for real neural embeddings (OpenAI/Cohere/local
/// sentence-transformers) + a hosted vector DB (Pinecone/pgvector/Qdrant) later is a
/// drop-in replacement — only this class changes, IKnowledgeBaseService and the
/// orchestrator stay untouched.
/// </summary>
public class VectorKnowledgeBaseService : IKnowledgeBaseService
{
    private readonly List<KnowledgeArticle> _articles;
    private readonly List<Dictionary<string, double>> _articleVectors;
    private readonly Dictionary<string, double> _idf;

    public VectorKnowledgeBaseService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "KnowledgeBase", "articles.json");
        var json = File.ReadAllText(path);

        var raw = JsonSerializer.Deserialize<List<RawArticle>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new();

        _articles = raw.Select(r => new KnowledgeArticle
        {
            Title = r.Title,
            Content = r.Content,
            Tags = r.Tags
        }).ToList();

        // Build IDF (inverse document frequency) across the whole corpus once.
        var docTokenSets = _articles
            .Select(a => Tokenize(a.Title + " " + a.Content + " " + string.Join(' ', a.Tags)))
            .ToList();

        var docCount = docTokenSets.Count;
        var docFrequency = new Dictionary<string, int>();
        foreach (var tokens in docTokenSets)
        {
            foreach (var term in tokens.Distinct())
            {
                docFrequency[term] = docFrequency.GetValueOrDefault(term) + 1;
            }
        }

        _idf = docFrequency.ToDictionary(
            kv => kv.Key,
            kv => Math.Log((double)docCount / (1 + kv.Value)) + 1.0); // smoothed idf, never zero/negative

        // Pre-compute each article's TF-IDF vector once at startup.
        _articleVectors = docTokenSets.Select(tokens => TfIdfVector(tokens)).ToList();
    }

    public Task<List<string>> SearchAsync(string query, string? category, int topK = 3, CancellationToken ct = default)
    {
        var queryVector = TfIdfVector(Tokenize(query + " " + category));

        var scored = _articles
            .Select((article, i) =>
            {
                var similarity = CosineSimilarity(queryVector, _articleVectors[i]);

                // Small boost when the predicted category matches a tag — keeps the
                // classifier's signal useful without letting it dominate pure semantic match.
                if (category is not null && article.Tags.Contains(category))
                {
                    similarity += 0.15;
                }

                return (Article: article, Score: similarity);
            })
            .Where(x => x.Score > 0.05) // filter out near-zero / no real overlap
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => $"{x.Article.Title}: {x.Article.Content}")
            .ToList();

        return Task.FromResult(scored);
    }

    private Dictionary<string, double> TfIdfVector(HashSet<string> tokens)
    {
        var vector = new Dictionary<string, double>();
        if (tokens.Count == 0) return vector;

        // Term frequency: within this short text, weight = 1/count (normalized).
        var tf = 1.0 / tokens.Count;
        foreach (var term in tokens)
        {
            var idf = _idf.GetValueOrDefault(term, Math.Log(_articles.Count + 1) + 1.0);
            vector[term] = tf * idf;
        }
        return vector;
    }

    private static double CosineSimilarity(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;

        double dot = 0;
        foreach (var (term, weight) in a)
        {
            if (b.TryGetValue(term, out var otherWeight))
            {
                dot += weight * otherWeight;
            }
        }

        var magA = Math.Sqrt(a.Values.Sum(v => v * v));
        var magB = Math.Sqrt(b.Values.Sum(v => v * v));
        if (magA == 0 || magB == 0) return 0;

        return dot / (magA * magB);
    }

    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(new[] { ' ', '.', ',', '\n', '\r', '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

    private class RawArticle
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }
}
