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
  private const string NO_KNOWLEDGE_BASE_ANSWER = "I don't have information about that in my knowledge base.";

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
      var relevantContext = await _contextSearchService.SearchRelevantContextAsync(conversation.Message, maxChunks: 5);
      var hasRelevantContext = !string.IsNullOrWhiteSpace(relevantContext)
        && await IsContextRelevantAsync(conversation.Message, relevantContext, cancellationToken);
      var prompt = hasRelevantContext
        ? CreatePrompt(conversation.Message, relevantContext)
        : CreateGeneralKnowledgePrompt(conversation.Message);
      _logger.LogInformation(
        hasRelevantContext
          ? "Using hybrid-search context for OpenAI model prompt"
          : "No relevant context found; using the general-knowledge OpenAI model prompt");

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
    var relevantContext = await _contextSearchService.SearchRelevantContextAsync(conversation.Message, maxChunks: 5);
    var hasRelevantContext = !string.IsNullOrWhiteSpace(relevantContext)
      && await IsContextRelevantAsync(conversation.Message, relevantContext, cancellationToken);
    var prompt = hasRelevantContext
      ? CreatePrompt(conversation.Message, relevantContext)
      : CreateGeneralKnowledgePrompt(conversation.Message);
    _logger.LogInformation(
      hasRelevantContext
        ? "Using hybrid-search context for streaming OpenAI model prompt"
        : "No relevant context found; using the general-knowledge streaming prompt");

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

  private async Task<bool> IsContextRelevantAsync(
    string question,
    string relevantContext,
    CancellationToken cancellationToken)
  {
    var relevanceModel = EnvironmentConfiguration.GetOptional("AZURE_OPENAI_RELEVANCE_MODEL_NAME");
    if (string.IsNullOrWhiteSpace(relevanceModel))
    {
      relevanceModel = EnvironmentConfiguration.GetRequired("AZURE_OPENAI_MODEL_NAME");
      _logger.LogWarning(
        "AZURE_OPENAI_RELEVANCE_MODEL_NAME is not configured; using the answer model for relevance checks. Configure a cheaper deployment for production.");
    }

    var judgePrompt =
      "You are a relevance classifier. Determine whether the context contains enough information to answer the question. " +
      "Answer only YES or NO. Do not answer the question.\n\n" +
      "Question:\n" + question +
      "\n\nContext:\n" + relevantContext;

    try
    {
      var options = new CreateResponseOptions
      {
        Model = relevanceModel,
        InputItems =
        {
          ResponseItem.CreateUserMessageItem(judgePrompt)
        }
      };

      var response = await _modelClient.CreateResponseAsync(options, cancellationToken);
      var decision = response?.Value?.GetOutputText()?.Trim();
      var isRelevant = string.Equals(decision, "YES", StringComparison.OrdinalIgnoreCase);

      _logger.LogInformation("Relevance judge decision: {Decision}", isRelevant ? "YES" : "NO");
      return isRelevant;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      _logger.LogError(exception, "Relevance judge failed; refusing to call the answer model");
      return false;
    }
  }

  private static string CreatePrompt(string question, string relevantContext)
  {
    return "You are a personal brand assistant for Szilard Ferencz. " +
      "Answer ONLY when the answer is supported by the knowledge-base context below. " +
      "Do not use general knowledge, assumptions, or information outside the context. " +
      "If the context does not contain enough information to answer the question, respond exactly with: \"" +
      NO_KNOWLEDGE_BASE_ANSWER +
      "\" Answer questions about Szilard in 1-3 clear sentences and refer to him in the third person.\n\n" +
      "Knowledge-base context:\n\n" +
      relevantContext +
      "\n\nUser question:\n" + question;
  }

  private static string CreateGeneralKnowledgePrompt(string question)
  {
    return "You are a helpful assistant for Szilard Ferencz. Answer the user's question directly in 1-3 clear sentences. " +
      "Use your general knowledge. When the question uses he, him, or his, interpret those pronouns as referring to Szilard and answer about him. " +
      "For questions about Szilard, only state facts you can support confidently; otherwise say you are unsure.\n\n" +
      "User question:\n" + question;
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
