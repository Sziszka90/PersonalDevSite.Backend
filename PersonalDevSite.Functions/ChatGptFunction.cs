using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

public class ChatGptFunction
{
  private readonly HttpClient _httpClient;
  private readonly string _userSummary;
  private readonly string _openAiApiKey;
  private readonly string _openAiApiUrl;

  public ChatGptFunction(IHttpClientFactory httpClientFactory, IConfiguration configuration)
  {
    _httpClient = httpClientFactory.CreateClient();

    // Ensure file exists in output folder
    var contentRoot = AppContext.BaseDirectory;
    var filePath = Path.Combine(contentRoot, "user_summary.txt");
    if (!File.Exists(filePath))
    {
      throw new FileNotFoundException("user_summary.txt not found in output directory.", filePath);
    }

    _userSummary = File.ReadAllText(filePath);

    _openAiApiKey = configuration["OPENAI_API_KEY"]
        ?? throw new InvalidOperationException("OPENAI_API_KEY is not set.");

    _openAiApiUrl = configuration["OPENAI_API_URL"]
        ?? "https://api.openai.com/v1/chat/completions";
  }

  [Function("ChatGptFunction")]
  public async Task<HttpResponseData> Run(
      [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
      FunctionContext executionContext)
  {
    var log = executionContext.GetLogger("ChatGptFunction");

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var data = JsonSerializer.Deserialize<JsonElement>(requestBody);

    JsonElement[] messages;
    if (data.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array)
    {
      var msgList = new List<JsonElement>();

      // Ensure system message exists
      if (msgs.GetArrayLength() == 0 || !msgs[0].TryGetProperty("role", out var role) || role.GetString() != "system")
      {
        msgList.Add(JsonDocument.Parse($"{{\"role\":\"system\",\"content\":\"{_userSummary}\"}}").RootElement);
      }

      foreach (var m in msgs.EnumerateArray())
      {
        msgList.Add(m);
      }

      messages = msgList.ToArray();
    }
    else
    {
      string? userMessage = data.TryGetProperty("message", out var msg) ? msg.GetString() : "Hello!";
      messages = new JsonElement[]
      {
                JsonDocument.Parse($"{{\"role\":\"system\",\"content\":\"{_userSummary}\"}}").RootElement,
                JsonDocument.Parse($"{{\"role\":\"user\",\"content\":\"{userMessage}\"}}").RootElement
      };
    }

    var openAiRequest = new
    {
      model = "gpt-3.5-turbo",
      messages = messages
    };

    // Explicit request building
    using var request = new HttpRequestMessage(HttpMethod.Post, _openAiApiUrl);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
    request.Content = new StringContent(JsonSerializer.Serialize(openAiRequest), Encoding.UTF8, "application/json");

    var openAiResponse = await _httpClient.SendAsync(request);
    var responseString = await openAiResponse.Content.ReadAsStringAsync();

    if (!openAiResponse.IsSuccessStatusCode)
    {
      log.LogError("OpenAI API error: {StatusCode} - {Body}", openAiResponse.StatusCode, responseString);
      var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
      await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "OpenAI API request failed." }));
      return errorResponse;
    }

    using var doc = JsonDocument.Parse(responseString);
    if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
    {
      log.LogWarning("No choices returned from OpenAI API.");
      var emptyResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
      await emptyResponse.WriteStringAsync(JsonSerializer.Serialize(new { reply = "" }));
      return emptyResponse;
    }

    var reply = choices[0].GetProperty("message").GetProperty("content").GetString();

    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
    response.Headers.Add("Content-Type", "application/json");
    await response.WriteStringAsync(JsonSerializer.Serialize(new { reply }));

    return response;
  }
}
