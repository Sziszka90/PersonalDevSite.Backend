using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var host = new HostBuilder()
  .ConfigureFunctionsWorkerDefaults()
  .ConfigureServices((context, services) =>
  {
    services.AddHttpClient();
  })
  .Build();

host.Run();

