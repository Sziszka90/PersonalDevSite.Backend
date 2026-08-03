using System;
using System.IO;
using System.Linq;
using System.Net;
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
  private readonly IOpenAIClient _openAIClient;
  private ILogger? _logger;

  public LLMProcessorFunction(IOpenAIClient openAIClient)
  {
    _openAIClient = openAIClient;
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

    if (AcceptsEventStream(req))
    {
      return await CreateStreamingResponse(req, conversation, req.FunctionContext.CancellationToken);
    }

    var responseData = await _openAIClient.PostAsync(conversation, req.FunctionContext.CancellationToken);

    if (responseData.IsSuccess)
    {
      if (responseData.Data is not null)
      {
        return CreateResponse(req, responseData.Data, System.Net.HttpStatusCode.OK);
      }
      else
      {
        _logger.LogError("OpenAI response data is null.");
        return CreateResponse(req, new { error = "OpenAI response data is null." }, System.Net.HttpStatusCode.InternalServerError);
      }
    }
    else
    {
      _logger.LogError($"OpenAI request failed: {responseData.Error}");
      return CreateResponse(req, new { error = responseData.Error }, System.Net.HttpStatusCode.InternalServerError);
    }
  }

  private async Task<HttpResponseData> CreateStreamingResponse(
    HttpRequestData req,
    ConversationDto conversation,
    System.Threading.CancellationToken cancellationToken)
  {
    var response = req.CreateResponse(HttpStatusCode.OK);
    response.Headers.Add("Content-Type", "text/event-stream; charset=utf-8");
    response.Headers.Add("Cache-Control", "no-cache");
    response.Headers.Add("X-Accel-Buffering", "no");
    AddCorsHeaders(response);

    try
    {
      await foreach (var delta in _openAIClient.StreamAsync(conversation, cancellationToken))
      {
        await WriteSseEventAsync(response, "message", new { delta }, cancellationToken);
      }

      await response.WriteStringAsync("event: done\ndata: [DONE]\n\n", cancellationToken);
      await response.Body.FlushAsync(cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _logger?.LogInformation("Streaming OpenAI response was canceled by the client.");
    }
    catch (Exception ex)
    {
      _logger?.LogError(ex, "Streaming OpenAI request failed.");
      await WriteSseEventAsync(response, "error", new { error = "An error occurred while processing the OpenAI request." }, System.Threading.CancellationToken.None);
    }

    return response;
  }

  private static bool AcceptsEventStream(HttpRequestData request)
  {
    return request.Headers.TryGetValues("Accept", out var values)
      && values.Any(value => value.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase));
  }

  private static async Task WriteSseEventAsync(
    HttpResponseData response,
    string eventName,
    object payload,
    System.Threading.CancellationToken cancellationToken)
  {
    var data = JsonSerializer.Serialize(payload);
    await response.WriteStringAsync($"event: {eventName}\ndata: {data}\n\n", cancellationToken);
    await response.Body.FlushAsync(cancellationToken);
  }

  private static void AddCorsHeaders(HttpResponseData response)
  {
    if (!response.Headers.Contains("Access-Control-Allow-Origin"))
    {
      response.Headers.Add("Access-Control-Allow-Origin", "*");
      response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
      response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
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
