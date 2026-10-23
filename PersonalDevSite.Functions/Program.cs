using System;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Embeddings;
using PersonalDevSite.Functions.Abstraction.Clients;
using PersonalDevSite.Functions.Clients;
using PersonalDevSite.Functions.Configuration;
using PersonalDevSite.Functions.Middleware;
using PersonalDevSite.Functions.Services;

var host = new HostBuilder()
  .ConfigureFunctionsWorkerDefaults(worker =>
  {
    worker.UseMiddleware<CustomCorsMiddleware>();
  })
  .ConfigureServices((context, services) =>
  {
    services.AddHttpClient();
    services.AddScoped<IOpenAIClient, OpenAIClient>();
    services.AddScoped(sp => CreateEmbeddingClient());
    services.AddScoped(sp => CreateSearchClient());
    services.AddScoped<IContextSearchService, ContextSearchService>();
  })
  .Build();

host.Run();

static EmbeddingClient CreateEmbeddingClient()
{
  var client = CreateAzureOpenAIClient();
  var deployment = EnvironmentConfiguration.GetRequired("AZURE_OPENAI_EMBEDDING_DEPLOYMENT");
  return client.GetEmbeddingClient(deployment);
}

static AzureOpenAIClient CreateAzureOpenAIClient()
{
  var endpoint = EnvironmentConfiguration.GetRequired("AZURE_OPENAI_ENDPOINT");
  var apiKey = EnvironmentConfiguration.GetRequiredSecret("AZURE_OPENAI_API_KEY");
  return new AzureOpenAIClient(new Uri(NormalizeAzureOpenAIEndpoint(endpoint)), new AzureKeyCredential(apiKey));
}

static string NormalizeAzureOpenAIEndpoint(string endpoint)
{
  return endpoint.TrimEnd('/').Replace("/openai/v1", string.Empty, StringComparison.OrdinalIgnoreCase)
    .Replace("/openai", string.Empty, StringComparison.OrdinalIgnoreCase) + "/";
}

static SearchClient CreateSearchClient()
{
  var endpoint = new Uri(EnvironmentConfiguration.GetRequired("AZURE_SEARCH_ENDPOINT"));
  var indexName = EnvironmentConfiguration.GetRequired("AZURE_SEARCH_INDEX_NAME");
  var apiKey = EnvironmentConfiguration.GetRequiredSecret("AZURE_SEARCH_API_KEY");
  return new SearchClient(endpoint, indexName, new AzureKeyCredential(apiKey));
}

