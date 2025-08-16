using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Azure.Functions.Worker.Http;

namespace PersonalDevSite.Functions.Middleware;

public class CustomCorsMiddleware : IFunctionsWorkerMiddleware
{
  public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
  {
    var httpReqData = await context.GetHttpRequestDataAsync();

    if (httpReqData != null && httpReqData.Method == "OPTIONS")
    {
      var response = httpReqData.CreateResponse(HttpStatusCode.OK);
      response.Headers.Add("Access-Control-Allow-Origin", "*");
      response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
      response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
      response.Headers.Add("Access-Control-Max-Age", "86400");
      await response.WriteStringAsync(string.Empty);
      context.GetInvocationResult().Value = response;
      return;
    }

    await next(context);

    var httpResData = context.GetHttpResponseData();
    if (httpResData != null)
    {
      httpResData.Headers.Add("Access-Control-Allow-Origin", "*");
      httpResData.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
      httpResData.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
    }
  }
}
