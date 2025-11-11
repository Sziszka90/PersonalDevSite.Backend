using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Embeddings;
using PersonalDevSite.Functions.Abstraction.Clients;
using PersonalDevSite.Functions.Clients;
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
    services.AddScoped<IChatGptClient, ChatGptClient>();
    services.AddScoped(sp =>
      new EmbeddingClient("text-embedding-3-small", Environment.GetEnvironmentVariable("OPENAI_API_KEY")));
    services.AddScoped<IContextSearchService, ContextSearchService>();
  })
  .Build();

host.Run();

