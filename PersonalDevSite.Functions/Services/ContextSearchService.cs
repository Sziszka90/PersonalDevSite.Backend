using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace PersonalDevSite.Functions.Services;

public class ContextSearchService : IContextSearchService
{
  private readonly ILogger<ContextSearchService> _logger;
  private readonly EmbeddingClient _embeddingClient;
  private readonly SearchClient _searchClient;

  public ContextSearchService(
    ILogger<ContextSearchService> logger,
    EmbeddingClient embeddingClient,
    SearchClient searchClient)
  {
    _logger = logger;
    _embeddingClient = embeddingClient;
    _searchClient = searchClient;
  }

  public async Task<string> SearchRelevantContextAsync(string query, int maxChunks = 3)
  {
    if (string.IsNullOrWhiteSpace(query))
    {
      _logger.LogWarning("Empty query provided to context search");
    }

    _logger.LogInformation("Generating embedding for user query");
    var embeddingResponse = await _embeddingClient.GenerateEmbeddingAsync(query);
    var vectorQuery = new VectorizedQuery(embeddingResponse.Value.ToFloats().ToArray())
    {
      KNearestNeighborsCount = maxChunks,
      Fields = { "embedding" }
    };

    var options = new SearchOptions
    {
      Size = maxChunks,
      VectorSearch = new VectorSearchOptions
      {
        Queries = { vectorQuery }
      }
    };
    options.Select.Add("content");

    var results = await _searchClient.SearchAsync<SearchDocument>(query, options);
    var chunks = new List<string>();
    await foreach (var result in results.Value.GetResultsAsync())
    {
      if (result.Document.TryGetValue("content", out var content) && !string.IsNullOrWhiteSpace(content?.ToString()))
      {
        chunks.Add(content.ToString()!);
      }
    }

    if (chunks.Count == 0)
    {
      _logger.LogInformation("No relevant chunks found, returning full summary");
      return "No information found";
    }

    _logger.LogInformation("Found {ChunkCount} relevant chunks in Azure AI Search", chunks.Count);
    return string.Join("\n\n", chunks);
  }
}
