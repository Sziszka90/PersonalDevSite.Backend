using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;
using PersonalDevSite.Functions.Abstraction.Clients;
using PersonalDevSite.Functions.Configuration;
using PersonalDevSite.Functions.Dtos;
using PersonalDevSite.Functions.Models;
using PersonalDevSite.Functions.Services;

namespace PersonalDevSite.Functions.Clients;

#pragma warning disable OPENAI001

public class OpenAIClient : IOpenAIClient
{
  private readonly ILogger<OpenAIClient> _logger;
  private readonly IContextSearchService _contextSearchService;
  private readonly ResponsesClient _modelClient;

  public OpenAIClient(ILogger<OpenAIClient> logger, IContextSearchService contextSearchService)
  {
    _logger = logger;
    _contextSearchService = contextSearchService;
    _modelClient = CreateModelClient();
  }

  public async Task<Result<ConversationDto>> PostAsync(ConversationDto conversation, CancellationToken cancellationToken = default)
  {
    try
    {
      var relevantContext = await _contextSearchService.SearchRelevantContextAsync(conversation.Message, maxChunks: 2);

      var prompt = CreatePrompt(conversation.Message, relevantContext);
      _logger.LogInformation(
        string.IsNullOrWhiteSpace(relevantContext)
          ? "No relevant context found; forwarding the full question to the OpenAI model"
          : "Using relevant context for OpenAI model prompt");

      var options = new CreateResponseOptions
      {
        Model = EnvironmentConfiguration.GetRequired("AZURE_OPENAI_MODEL_NAME"),
        InputItems =
        {
          ResponseItem.CreateUserMessageItem(prompt)
        }
      };

      var response = await _modelClient.CreateResponseAsync(options, cancellationToken);

      if (response?.Value is null)
      {
        return new Result<ConversationDto>
        {
          Error = "Failed to parse OpenAI response."
        };
      }

      return new Result<ConversationDto>
      {
        Data = new ConversationDto
        {
          Message = response.Value.GetOutputText()
        }
      };
    }
    catch (Exception ex)
    {
      return new Result<ConversationDto>
      {
        Error = $"An error occurred while processing the OpenAI request: {ex.Message}"
      };
    }
  }

  public async IAsyncEnumerable<string> StreamAsync(
    ConversationDto conversation,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    var relevantContext = await _contextSearchService.SearchRelevantContextAsync(conversation.Message, maxChunks: 2);

    var prompt = CreatePrompt(conversation.Message, relevantContext);
    _logger.LogInformation(
      string.IsNullOrWhiteSpace(relevantContext)
        ? "No relevant context found; forwarding the full question to the OpenAI model"
        : "Using relevant context for streaming OpenAI model prompt");

    var options = new CreateResponseOptions
    {
      Model = EnvironmentConfiguration.GetRequired("AZURE_OPENAI_MODEL_NAME"),
      StreamingEnabled = true,
      InputItems =
      {
        ResponseItem.CreateUserMessageItem(prompt)
      }
    };

    await foreach (var update in _modelClient.CreateResponseStreamingAsync(options, cancellationToken))
    {
      if (update is StreamingResponseOutputTextDeltaUpdate textUpdate
        && !string.IsNullOrEmpty(textUpdate.Delta))
      {
        yield return textUpdate.Delta;
      }
    }
  }

  private static string CreatePrompt(string question, string relevantContext)
  {
    if (string.IsNullOrWhiteSpace(relevantContext))
    {
      return "You are a personal brand assistant who answers questions about Szilard Ferencz. " +
        "Answer in 1-3 clear sentences, always refer to Szilard in the third person. " +
        "No relevant context was found, so answer the user's question using your general knowledge " +
        "and be transparent if you are unsure.\n\nUser question:\n" + question;
    }

    return "You are a personal brand assistant who answers questions about Szilard Ferencz. " +
      "Answer in 1-3 clear sentences, always refer to Szilard in the third person, " +
      "and use ONLY the following relevant context:\n\n" +
      relevantContext +
      "\n\nUser question:\n" + question;
  }

  private static ResponsesClient CreateModelClient()
  {
    var endpoint = EnvironmentConfiguration.GetRequired("AZURE_OPENAI_MODEL_ENDPOINT");
    var apiKey = EnvironmentConfiguration.GetRequiredSecret("AZURE_OPENAI_API_KEY");

    return new ResponsesClient(
      credential: new ApiKeyCredential(apiKey),
      options: new ResponsesClientOptions
      {
        Endpoint = new Uri(endpoint)
      });
  }
}
