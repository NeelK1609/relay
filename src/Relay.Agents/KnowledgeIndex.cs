using System.Numerics.Tensors;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Embeddings;
using Relay.Domain;

namespace Relay.Agents;

public sealed record KnowledgeHit(Guid Id, string Title, CaseCategory Category, string Body, double Score);

/// <summary>
/// In-memory vector index over help articles. Fine for a small help centre;
/// swap for Azure AI Search when the article count grows.
/// </summary>
public sealed class KnowledgeIndex(EmbeddingClient embeddings, IServiceScopeFactory scopes)
{
    private sealed record Entry(KnowledgeArticle Article, float[] Vector);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<Guid, Entry> _entries = [];

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(string query, int top, CancellationToken ct)
    {
        var entries = await RefreshAsync(ct);
        if (entries.Count == 0) return [];

        var queryVector = (await embeddings.GenerateEmbeddingAsync(query, cancellationToken: ct)).Value.ToFloats();
        return entries.Values
            .Select(e => new KnowledgeHit(e.Article.Id, e.Article.Title, e.Article.Category, e.Article.Body,
                TensorPrimitives.CosineSimilarity(queryVector.Span, e.Vector)))
            .OrderByDescending(h => h.Score)
            .Take(top)
            .ToList();
    }

    private async Task<Dictionary<Guid, Entry>> RefreshAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var crm = scope.ServiceProvider.GetRequiredService<ICrmStore>();
            var articles = await crm.SearchKnowledgeAsync(null, null, ct);

            var stale = articles
                .Where(a => !_entries.TryGetValue(a.Id, out var e) || e.Article.UpdatedAt != a.UpdatedAt)
                .ToList();
            var next = articles
                .Where(a => _entries.ContainsKey(a.Id) && !stale.Contains(a))
                .ToDictionary(a => a.Id, a => _entries[a.Id]);

            if (stale.Count > 0)
            {
                var vectors = await embeddings.GenerateEmbeddingsAsync(
                    stale.Select(a => $"{a.Title}\n{a.Body}"), cancellationToken: ct);
                for (var i = 0; i < stale.Count; i++)
                {
                    next[stale[i].Id] = new Entry(stale[i], vectors.Value[i].ToFloats().ToArray());
                }
            }

            _entries = next;
            return _entries;
        }
        finally
        {
            _gate.Release();
        }
    }
}
