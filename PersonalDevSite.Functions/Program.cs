
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalDevSite.Functions.Abstraction;
using PersonalDevSite.Functions.Clients;

var host = new HostBuilder()
  .ConfigureFunctionsWorkerDefaults()
  .ConfigureServices((context, services) =>
  {
    services.AddHttpClient();
    services.AddScoped<IChatGptClient, ChatGptClient>();
  })
  .Build();

host.Run();

