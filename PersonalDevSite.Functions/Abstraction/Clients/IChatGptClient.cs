using System.Threading;
using System.Threading.Tasks;
using PersonalDevSite.Functions.Dtos;
using PersonalDevSite.Functions.Models;

namespace PersonalDevSite.Functions.Abstraction.Clients;

public interface IChatGptClient
{
  /// <summary>
  /// Sends a conversation request to the ChatGPT API.
  /// </summary>
  /// <param name="conversation"></param>
  /// <param name="cancellationToken"></param>
  /// <returns>Conversation response</returns>
  Task<Result<ConversationDto>> PostAsync(ConversationDto conversation, CancellationToken cancellationToken = default);
}
