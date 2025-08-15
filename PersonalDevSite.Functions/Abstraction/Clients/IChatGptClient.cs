using System.Threading;
using System.Threading.Tasks;
using PersonalDevSite.Functions.Models;

namespace PersonalDevSite.Functions.Abstraction;

public interface IChatGptClient
{
  /// <summary>
  /// Sends a conversation request to the ChatGPT API.
  /// </summary>
  /// <param name="converation"></param>
  /// <param name="cancellationToken"></param>
  /// <returns>Conversation response</returns>
  Task<Result<ConversationDto>> PostAsync(ConversationDto converation, CancellationToken cancellationToken = default);
}
