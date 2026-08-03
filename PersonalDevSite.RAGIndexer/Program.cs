using System.Text.Json;

var settings = LoadSettings();
var searchEndpoint = GetRequiredSetting("AZURE_SEARCH_ENDPOINT", settings);
var openAiEndpoint = GetRequiredSetting("AZURE_OPENAI_ENDPOINT", settings);
var embeddingDeployment = GetRequiredSetting("AZURE_OPENAI_EMBEDDING_DEPLOYMENT", settings);
var indexName = GetRequiredSetting("AZURE_SEARCH_INDEX_NAME", settings);

var summaryPath = args.Length > 0
  ? Path.GetFullPath(args[0])
  : Path.Combine(AppContext.BaseDirectory, "user_summary.txt");

if (!File.Exists(summaryPath))
{
  throw new FileNotFoundException("The summary file could not be found.", summaryPath);
}

var content = await File.ReadAllTextAsync(summaryPath);
var chunks = ChunkContent(content);

if (chunks.Count == 0)
{
  throw new InvalidOperationException("The summary file does not contain any text to index.");
}

var embeddingClient = CreateEmbeddingClient(openAiEndpoint, embeddingDeployment, settings);
var searchClient = CreateSearchClient(searchEndpoint, indexName, settings);

await ClearIndexAsync(searchClient);

Console.WriteLine($"Embedding {chunks.Count} chunks for existing index '{indexName}'...");

var documents = new List<SearchDocument>(chunks.Count);

for (var index = 0; index < chunks.Count; index++)
{
  var embedding = await embeddingClient.GenerateEmbeddingAsync(chunks[index]);
  documents.Add(new SearchDocument
  {
    ["id"] = $"chunk-{index:D4}",
    ["content"] = chunks[index],
    ["embedding"] = embedding.Value.ToFloats().ToArray()
  });
}

await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(documents));

Console.WriteLine($"Indexed {documents.Count} chunks in '{indexName}'.");

static EmbeddingClient CreateEmbeddingClient(string endpoint, string deployment, IReadOnlyDictionary<string, string?> settings)
{
  var apiKey = GetSetting("AZURE_OPENAI_API_KEY", settings);
  var client = string.IsNullOrWhiteSpace(apiKey)
    ? new AzureOpenAIClient(new Uri(NormalizeAzureOpenAIEndpoint(endpoint)), new DefaultAzureCredential())
    : new AzureOpenAIClient(new Uri(NormalizeAzureOpenAIEndpoint(endpoint)), new AzureKeyCredential(apiKey));

  return client.GetEmbeddingClient(deployment);
}

static string NormalizeAzureOpenAIEndpoint(string endpoint)
{
  return endpoint.TrimEnd('/').Replace("/openai/v1", string.Empty, StringComparison.OrdinalIgnoreCase)
    .Replace("/openai", string.Empty, StringComparison.OrdinalIgnoreCase) + "/";
}

static SearchClient CreateSearchClient(string endpoint, string indexName, IReadOnlyDictionary<string, string?> settings)
{
  var apiKey = GetSetting("AZURE_SEARCH_API_KEY", settings);
  return string.IsNullOrWhiteSpace(apiKey)
    ? new SearchClient(new Uri(endpoint), indexName, new DefaultAzureCredential())
    : new SearchClient(new Uri(endpoint), indexName, new AzureKeyCredential(apiKey));
}

static async Task ClearIndexAsync(SearchClient searchClient)
{
  var options = new SearchOptions
  {
    Size = 1000
  };
  options.Select.Add("id");

  var results = await searchClient.SearchAsync<SearchDocument>("*", options);
  var keys = new List<string>();

  await foreach (var result in results.Value.GetResultsAsync())
  {
    if (result.Document.TryGetValue("id", out var id) && id is string key && !string.IsNullOrWhiteSpace(key))
    {
      keys.Add(key);
    }
  }

  foreach (var batchKeys in keys.Chunk(1000))
  {
    await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Delete("id", batchKeys));
  }

  Console.WriteLine($"Removed {keys.Count} existing documents from the search index.");
}

static List<string> ChunkContent(string content)
{
  const int maxChunkLength = 1500;
  const int overlapLength = 300;
  var paragraphs = content.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries);
  var chunks = new List<string>();
  var current = string.Empty;

  foreach (var paragraph in paragraphs.Select(value => value.Trim()).Where(value => value.Length > 0))
  {
    if (paragraph.Length <= maxChunkLength)
    {
      AddUnit(paragraph);
      continue;
    }

    foreach (var word in paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
    {
      AddUnit(word);
    }
  }

  if (current.Length > 0)
  {
    chunks.Add(current);
  }

  return chunks;

  void AddUnit(string unit)
  {
    var separator = current.Length == 0 ? string.Empty : Environment.NewLine + Environment.NewLine;
    if (current.Length == 0)
    {
      current = unit;
      return;
    }

    if (current.Length + separator.Length + unit.Length <= maxChunkLength)
    {
      current += separator + unit;
      return;
    }

    if (current.Length > 0)
    {
      chunks.Add(current);
    }

    var overlap = current.Length > overlapLength ? current[^overlapLength..] : current;
    current = overlap.Length > 0 && overlap.Length + 1 + unit.Length <= maxChunkLength
      ? overlap + " " + unit
      : unit;
  }
}

static Dictionary<string, string?> LoadSettings()
{
  var settings = LoadJsonSettings(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
  var developmentSettings = LoadJsonSettings(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"));

  foreach (var setting in developmentSettings)
  {
    settings[setting.Key] = setting.Value;
  }

  return settings;
}

static Dictionary<string, string?> LoadJsonSettings(string settingsPath)
{
  if (!File.Exists(settingsPath))
  {
    return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
  }

  using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
  return document.RootElement.EnumerateObject()
    .Where(property => property.Value.ValueKind == JsonValueKind.String)
    .ToDictionary(property => property.Name, property => property.Value.GetString(), StringComparer.OrdinalIgnoreCase);
}

static string? GetSetting(string name, IReadOnlyDictionary<string, string?> settings)
{
  var environmentValue = Environment.GetEnvironmentVariable(name);
  return string.IsNullOrWhiteSpace(environmentValue)
    ? settings.GetValueOrDefault(name)
    : environmentValue;
}

static string GetRequiredSetting(string name, IReadOnlyDictionary<string, string?> settings)
{
  var value = GetSetting(name, settings);
  return string.IsNullOrWhiteSpace(value)
    ? throw new InvalidOperationException($"Set {name} in appsettings.Development.json or as an environment variable before running the indexer.")
    : value;
}
