using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PersonalDevSite.Functions.Abstraction;

namespace PersonalDevSite.Functions;

public class LLMProcessorFunction
{
  private readonly IChatGptClient _chatGptClient;

  public LLMProcessorFunction(IChatGptClient chatGptClient)
  {
    _chatGptClient = chatGptClient;
  }

  [Function("LLMProcessorFunction")]
  public async Task<HttpResponseData> Run(
      [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
      FunctionContext executionContext)
  {
    var log = executionContext.GetLogger("LLMProcessorFunction");

    var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

    if (string.IsNullOrEmpty(requestBody))
    {
      log.LogError("Request body is empty.");
      var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
      await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Request body cannot be empty." }));
      return errorResponse;
    }

    ConversationDto? conversation;

    try
    {
      var options = new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      };

      conversation = JsonSerializer.Deserialize<ConversationDto>(requestBody, options);

      if (conversation == null)
      {
        log.LogError("Deserialized ConversationDto is null.");
        var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Invalid request body." }));
        return errorResponse;
      }
    }
    catch (JsonException ex)
    {
      log.LogError(ex, "Failed to deserialize request body.");
      var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
      await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Invalid request body." }));
      return errorResponse;
    }

    if (string.IsNullOrEmpty(conversation.Message))
    {
      log.LogError("Conversation message is empty.");
      var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
      await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Conversation message cannot be empty." }));
      return errorResponse;
    }

    var responseData = await _chatGptClient.PostAsync(conversation, req.FunctionContext.CancellationToken);

    var jsonOptions = new JsonSerializerOptions
    {
      Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    if (responseData.IsSuccess)
    {
      var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
      await response.WriteStringAsync(JsonSerializer.Serialize(responseData.Data, jsonOptions));
      return response;
    }
    else
    {
      log.LogError($"ChatGPT request failed: {responseData.Error}");
      var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
      await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = responseData.Error }, jsonOptions));
      return errorResponse;
    }
  }
}
