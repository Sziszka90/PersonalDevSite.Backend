using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PersonalDevSite.Functions.Abstraction.Clients;
using PersonalDevSite.Functions.Dtos;
using PersonalDevSite.Functions.Models;

namespace PersonalDevSite.Functions;

public class LLMProcessorFunction
{
  private readonly IChatGptClient _chatGptClient;
  private ILogger? _logger;

  public LLMProcessorFunction(IChatGptClient chatGptClient)
  {
    _chatGptClient = chatGptClient;
  }

  [Function("LLMProcessorFunction")]
  public async Task<HttpResponseData> Run(
      [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
      FunctionContext executionContext)
  {
    _logger = executionContext.GetLogger("LLMProcessorFunction");

    var conversationResult = await CreateConversationDto(req);

    if (!conversationResult.IsSuccess)
    {
      _logger.LogError(conversationResult.Error);
      return CreateResponse(req, new { error = conversationResult.Error }, System.Net.HttpStatusCode.BadRequest);
    }

    var conversation = conversationResult.Data!;

    var responseData = await _chatGptClient.PostAsync(conversation, req.FunctionContext.CancellationToken);

    if (responseData.IsSuccess)
    {
      if (responseData.Data is not null)
      {
        return CreateResponse(req, responseData.Data, System.Net.HttpStatusCode.OK);
      }
      else
      {
        _logger.LogError("ChatGPT response data is null.");
        return CreateResponse(req, new { error = "ChatGPT response data is null." }, System.Net.HttpStatusCode.InternalServerError);
      }
    }
    else
    {
      _logger.LogError($"ChatGPT request failed: {responseData.Error}");
      return CreateResponse(req, new { error = responseData.Error }, System.Net.HttpStatusCode.InternalServerError);
    }
  }

  private HttpResponseData CreateResponse(HttpRequestData req, object payload, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
  {
    var response = req.CreateResponse(statusCode);
    response.Headers.Add("Content-Type", "application/json");
    var jsonOptions = new JsonSerializerOptions
    {
      Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    response.WriteString(JsonSerializer.Serialize(payload, jsonOptions));
    return response;
  }

  private async Task<Result<ConversationDto>> CreateConversationDto(HttpRequestData request)
  {
    var requestBody = await new StreamReader(request.Body).ReadToEndAsync();

    if (string.IsNullOrEmpty(requestBody))
    {
      _logger?.LogError("Request body is empty.");
      return new Result<ConversationDto>
      {
        Error = "Request body cannot be empty."
      };
    }

    ConversationDto? conversation;
    try
    {
      var options = new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      };

      conversation = JsonSerializer.Deserialize<ConversationDto>(requestBody, options);

      if (conversation is null || string.IsNullOrEmpty(conversation.Message))
      {
        _logger?.LogError("Deserialized ConversationDto is null or empty.");
        return new Result<ConversationDto>
        {
          Error = "Invalid request."
        };
      }
      return new Result<ConversationDto>
      {
        Data = conversation
      };
    }
    catch (JsonException ex)
    {
      _logger?.LogError(ex, "Failed to deserialize request body.");
      return new Result<ConversationDto>
      {
        Error = "Invalid request."
      };
    }
  }
}
