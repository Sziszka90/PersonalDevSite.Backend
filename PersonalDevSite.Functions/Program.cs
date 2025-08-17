using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalDevSite.Functions.Abstraction.Clients;
using PersonalDevSite.Functions.Clients;
using PersonalDevSite.Functions.Middleware;

var host = new HostBuilder()
  .ConfigureFunctionsWorkerDefaults(worker =>
  {
    worker.UseMiddleware<CustomCorsMiddleware>();
  })
  .ConfigureServices((context, services) =>
  {
    services.AddHttpClient();
    services.AddScoped<IChatGptClient, ChatGptClient>();
  })
  .Build();

host.Run();

