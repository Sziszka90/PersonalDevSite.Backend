using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;
using PersonalDevSite.Functions.Models;

namespace PersonalDevSite.Functions.Services;

public class ContextSearchService : IContextSearchService
{
  private readonly ILogger<ContextSearchService> _logger;
  private readonly string _summaryContent;
  private readonly EmbeddingClient _embeddingClient;
  private readonly List<string> _chunks;
  private readonly List<ChunkEmbedding> _chunkEmbeddings = [];
  private bool _isInitialized = false;

  public ContextSearchService(ILogger<ContextSearchService> logger, EmbeddingClient embeddingClient)
  {
    _logger = logger;
    _embeddingClient = embeddingClient;
    _summaryContent = ReadSummary();
    _chunks = ChunkContent(_summaryContent);
  }

  public async Task<string> SearchRelevantContextAsync(string query, int maxChunks = 3)
  {
    if (string.IsNullOrWhiteSpace(query))
    {
      _logger.LogWarning("Empty query provided to context search");
      return _summaryContent;
    }

    if (!_isInitialized)
    {
      await InitializeEmbeddingsAsync();
    }

    _logger.LogInformation("Generating embedding for user query");
    var queryEmbeddingResponse = await _embeddingClient.GenerateEmbeddingAsync(query);
    var queryEmbedding = queryEmbeddingResponse.Value.ToFloats().ToArray();

    const float similarityThreshold = 0.2f;

    var scoredChunks = _chunkEmbeddings
      .Select(ce => new
      {
        Chunk = ce.Text,
        Score = CalculateCosineSimilarity(queryEmbedding, ce.Embedding)
      })
      .Where(x => x.Score >= similarityThreshold)
      .OrderByDescending(x => x.Score)
      .Take(maxChunks)
      .ToList();

    if (scoredChunks.Count == 0)
    {
      _logger.LogInformation("No relevant chunks found above threshold, returning full summary");
      return _summaryContent;
    }

    var relevantContext = string.Join("\n\n", scoredChunks.Select(x => x.Chunk));
    _logger.LogInformation($"Found {scoredChunks.Count} relevant chunks with scores: {string.Join(", ", scoredChunks.Select(x => x.Score.ToString("F3")))}");

    return relevantContext;
  }

  private async Task InitializeEmbeddingsAsync()
  {
    _logger.LogInformation($"Initializing embeddings for {_chunks.Count} chunks");

    foreach (var chunk in _chunks)
    {
      var embeddingResponse = await _embeddingClient.GenerateEmbeddingAsync(chunk);
      var embedding = embeddingResponse.Value.ToFloats().ToArray();

      _chunkEmbeddings.Add(new ChunkEmbedding
      {
        Text = chunk,
        Embedding = embedding
      });
    }

    _isInitialized = true;
    _logger.LogInformation("Embeddings initialized successfully");
  }

  private float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
  {
    if (vectorA.Length != vectorB.Length)
    {
      throw new ArgumentException("Vectors must have the same length");
    }

    float dotProduct = 0;
    float magnitudeA = 0;
    float magnitudeB = 0;

    for (int i = 0; i < vectorA.Length; i++)
    {
      dotProduct += vectorA[i] * vectorB[i];
      magnitudeA += vectorA[i] * vectorA[i];
      magnitudeB += vectorB[i] * vectorB[i];
    }

    magnitudeA = (float)Math.Sqrt(magnitudeA);
    magnitudeB = (float)Math.Sqrt(magnitudeB);

    if (magnitudeA == 0 || magnitudeB == 0)
    {
      return 0;
    }

    return dotProduct / (magnitudeA * magnitudeB);
  }

  private List<string> ChunkContent(string content)
  {
    var paragraphs = content.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

    var chunks = new List<string>();
    foreach (var paragraph in paragraphs)
    {
      var trimmed = paragraph.Trim();
      if (!string.IsNullOrWhiteSpace(trimmed))
      {
        chunks.Add(trimmed);
      }
    }

    if (chunks.Count <= 1)
    {
      var sentences = content.Split(new[] { ". ", ".\n", ".\r\n" }, StringSplitOptions.RemoveEmptyEntries);
      chunks = sentences
        .Select(s => s.Trim() + (s.EndsWith(".") ? "" : "."))
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .ToList();
    }

    _logger.LogInformation($"Created {chunks.Count} chunks from summary content");
    return chunks;
  }

  private string ReadSummary()
  {
    var contentRoot = AppContext.BaseDirectory;
    var filePath = Path.Combine(contentRoot, "user_summary.txt");

    if (!File.Exists(filePath))
    {
      throw new FileNotFoundException("user_summary.txt not found in output directory.", filePath);
    }

    return File.ReadAllText(filePath);
  }
}
