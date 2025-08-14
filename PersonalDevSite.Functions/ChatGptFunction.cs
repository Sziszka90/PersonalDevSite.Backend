using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace PersonalDevSite.Functions;

public static class ChatGptFunction
{
  // No session store; frontend must send full conversation history
  private static readonly string _userSummary = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "user_summary.txt"));
  private static readonly string _openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");
  private static readonly string _openAiApiUrl = Environment.GetEnvironmentVariable("OPENAI_API_URL") ?? "https://api.openai.com/v1/chat/completions";

  [FunctionName("ChatGptFunction")]
  public static async Task<IActionResult> Run(
      [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
      ILogger log)
  {
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var data = JsonSerializer.Deserialize<JsonElement>(requestBody);
    // Expect frontend to send full conversation history as 'messages' array
    JsonElement[] messages;
    if (data.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array)
    {
      var msgList = new System.Collections.Generic.List<JsonElement>();
      // Always prepend system summary if not present
      if (msgs.GetArrayLength() == 0 || !msgs[0].TryGetProperty("role", out var role) || role.GetString() != "system")
      {
        msgList.Add(JsonDocument.Parse($"{{\"role\":\"system\",\"content\":\"{_userSummary}\"}} ").RootElement);
      }
      foreach (var m in msgs.EnumerateArray())
      {
        msgList.Add(m);
      }
      messages = msgList.ToArray();
    }
    else
    {
      // If no messages provided, start with system and user message
      string? userMessage = data.TryGetProperty("message", out var msg) ? msg.GetString() : "Hello!";
      messages = new JsonElement[] {
        JsonDocument.Parse($"{{\"role\":\"system\",\"content\":\"{_userSummary}\"}} ").RootElement,
        JsonDocument.Parse($"{{\"role\":\"user\",\"content\":\"{userMessage}\"}} ").RootElement
      };
    }

    // Prepare OpenAI request
    var openAiRequest = new
    {
      model = "gpt-3.5-turbo",
      messages = messages
    };
    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiApiKey}");
    var content = new StringContent(JsonSerializer.Serialize(openAiRequest), Encoding.UTF8, "application/json");
    var openAiResponse = await httpClient.PostAsync(_openAiApiUrl, content);
    var responseString = await openAiResponse.Content.ReadAsStringAsync();
    using var doc = JsonDocument.Parse(responseString);
    var reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

    // Return reply only; frontend manages session and history
    return new OkObjectResult(new { reply });
  }
}

