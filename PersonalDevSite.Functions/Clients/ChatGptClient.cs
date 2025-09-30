using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Chat;
using PersonalDevSite.Functions.Abstraction.Clients;
using PersonalDevSite.Functions.Dtos;
using PersonalDevSite.Functions.Models;

namespace PersonalDevSite.Functions.Clients;

public class ChatGptClient : IChatGptClient
{
  private string? _userSummary;

  public ChatGptClient()
  {
    Initialization();
  }

  public async Task<Result<ConversationDto>> PostAsync(ConversationDto conversation, CancellationToken cancellationToken = default)
  {
    try
    {
      var client = new ChatClient(model: "gpt-4.1", apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

      var messages = new List<ChatMessage>
      {
        new SystemChatMessage(
          "You are a personal brand assistant for Szilard Ferencz. You know his professional background, skills, portfolio projects, and career interests. Answer user questions in 1–3 clear sentences, keeping responses concise, informative, and aligned with [Name]’s experience and personal brand. Avoid long explanations and stay focused on their expertise and achievements. Here's the summary about him: " + _userSummary
        ),
        new UserChatMessage(conversation.Message)
      };

      var response = await client.CompleteChatAsync(messages, cancellationToken: cancellationToken);

      if (response is null)
      {
        return new Result<ConversationDto>
        {
          Error = "Failed to parse ChatGPT response."
        };
      }

      return new Result<ConversationDto>
      {
        Data = new ConversationDto
        {
          Message = response.Value.Content[0].Text
        },
      };
    }
    catch (Exception ex)
    {
      return new Result<ConversationDto>
      {
        Error = $"An error occurred while processing the request: {ex.Message}"
      };
    }
  }

  private void Initialization()
  {
    var contentRoot = AppContext.BaseDirectory;
    var filePath = Path.Combine(contentRoot, "user_summary.txt");

    if (!File.Exists(filePath))
    {
      throw new FileNotFoundException("user_summary.txt not found in output directory.", filePath);
    }

    _userSummary = File.ReadAllText(filePath);
  }
}
