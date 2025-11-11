using System.Threading.Tasks;

namespace PersonalDevSite.Functions.Services;

public interface IContextSearchService
{
  Task<string> SearchRelevantContextAsync(string query, int maxChunks = 3);
}
