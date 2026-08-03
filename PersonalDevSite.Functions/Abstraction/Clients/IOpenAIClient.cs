using System.Threading;
using System.Threading.Tasks;
using PersonalDevSite.Functions.Dtos;
using PersonalDevSite.Functions.Models;

namespace PersonalDevSite.Functions.Abstraction.Clients;

public interface IOpenAIClient
{
  Task<Result<ConversationDto>> PostAsync(ConversationDto conversation, CancellationToken cancellationToken = default);
}