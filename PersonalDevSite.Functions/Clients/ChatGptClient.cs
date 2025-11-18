using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using PersonalDevSite.Functions.Abstraction.Clients;
using PersonalDevSite.Functions.Dtos;
using PersonalDevSite.Functions.Models;
using PersonalDevSite.Functions.Services;

namespace PersonalDevSite.Functions.Clients;

public class ChatGptClient : IChatGptClient
{
  private readonly ILogger<ChatGptClient> _logger;
  private readonly IContextSearchService _contextSearchService;

  public ChatGptClient(ILogger<ChatGptClient> logger, IContextSearchService contextSearchService)
  {
    _logger = logger;
    _contextSearchService = contextSearchService;
  }

  public async Task<Result<ConversationDto>> PostAsync(ConversationDto conversation, CancellationToken cancellationToken = default)
  {
    try
    {
      var relevantContext = await _contextSearchService.SearchRelevantContextAsync(conversation.Message, maxChunks: 2);

      _logger.LogInformation("Using relevant context for ChatGPT prompt");

      var client = new ChatClient(model: "gpt-4o-mini", apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

      var messages = new List<ChatMessage>
      {
        new SystemChatMessage(
          "You are a personal brand assistant who answers questions about Szilard Ferencz. Your role is to provide helpful information about his professional background, skills, portfolio projects, and career interests. Answer user questions in 1–3 clear sentences, keeping responses concise, informative, and aligned with Szilard's experience and personal brand. Avoid long explanations and stay focused on his expertise and achievements. Always refer to Szilard in the third person (e.g., 'Szilard has...', 'He specializes in...'). Use ONLY the following relevant context to answer questions:\n\n" + relevantContext
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
}
