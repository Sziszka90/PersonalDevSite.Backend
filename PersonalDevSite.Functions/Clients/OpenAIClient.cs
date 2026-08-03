using System;
using System.ClientModel;
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

      _logger.LogInformation("Using relevant context for OpenAI model prompt");

      var options = new CreateResponseOptions
      {
        Model = EnvironmentConfiguration.GetRequired("AZURE_OPENAI_MODEL_NAME"),
        InputItems =
        {
          ResponseItem.CreateUserMessageItem(
            "You are a personal brand assistant who answers questions about Szilard Ferencz. " +
            "Answer in 1-3 clear sentences, always refer to Szilard in the third person, " +
            "and use ONLY the following relevant context:\n\n" +
            relevantContext +
            "\n\nUser question:\n" + conversation.Message)
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
