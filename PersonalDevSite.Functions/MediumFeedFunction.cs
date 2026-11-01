using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace PersonalDevSite.Functions;

public class MediumFeedFunction
{
  private const string FeedUrl = "https://medium.com/feed/@szilard.fer";
  private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
  private static readonly SemaphoreSlim CacheLock = new(1, 1);
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };
  private static readonly Regex ImageSourceRegex = new(
    "<img[^>]+src=[\"']([^\"']+)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

  private static MediumFeedItem[]? _cachedItems;
  private static DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;

  private readonly IHttpClientFactory _httpClientFactory;

  public MediumFeedFunction(IHttpClientFactory httpClientFactory)
  {
    _httpClientFactory = httpClientFactory;
  }

  [Function("MediumFeedFunction")]
  public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData request,
    FunctionContext executionContext)
  {
    var logger = executionContext.GetLogger("MediumFeedFunction");

    try
    {
      var items = await GetFeedItemsAsync(logger, request.FunctionContext.CancellationToken);
      var response = request.CreateResponse(HttpStatusCode.OK);
      response.Headers.Add("Content-Type", "application/json; charset=utf-8");
      response.Headers.Add("Cache-Control", "public, max-age=300");
      await response.WriteStringAsync(JsonSerializer.Serialize(new { items }, JsonOptions));
      return response;
    }
    catch (Exception exception)
    {
      logger.LogError(exception, "Failed to retrieve the Medium RSS feed.");
      var response = request.CreateResponse(HttpStatusCode.BadGateway);
      response.Headers.Add("Content-Type", "application/json; charset=utf-8");
      response.Headers.Add("Cache-Control", "no-store");
      await response.WriteStringAsync("{\"error\":\"The blog feed is temporarily unavailable.\"}");
      return response;
    }
  }

  private async Task<MediumFeedItem[]> GetFeedItemsAsync(ILogger logger, CancellationToken cancellationToken)
  {
    if (_cachedItems is not null && DateTimeOffset.UtcNow < _cacheExpiresAt)
    {
      return _cachedItems;
    }

    await CacheLock.WaitAsync(cancellationToken);
    try
    {
      if (_cachedItems is not null && DateTimeOffset.UtcNow < _cacheExpiresAt)
      {
        return _cachedItems;
      }

      try
      {
        var client = _httpClientFactory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
        message.Headers.UserAgent.ParseAdd("PersonalDevSite/1.0");
        using var response = await client.SendAsync(
          message,
          HttpCompletionOption.ResponseHeadersRead,
          cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var items = ParseFeed(xml);
        if (items.Length == 0)
        {
          throw new InvalidOperationException("The Medium RSS feed contained no usable posts.");
        }

        _cachedItems = items;
        _cacheExpiresAt = DateTimeOffset.UtcNow.Add(CacheDuration);
        return items;
      }
      catch (Exception exception) when (_cachedItems is not null && exception is not OperationCanceledException)
      {
        logger.LogWarning(exception, "Medium RSS refresh failed; serving the last cached feed.");
        _cacheExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1);
        return _cachedItems;
      }
    }
    finally
    {
      CacheLock.Release();
    }
  }

  private static MediumFeedItem[] ParseFeed(string xml)
  {
    var document = XDocument.Parse(xml);
    return document
      .Descendants()
      .Where(element => element.Name.LocalName == "item")
      .Select(ParseItem)
      .Where(item => item is not null)
      .Select(item => item!)
      .ToArray();
  }

  private static MediumFeedItem? ParseItem(XElement item)
  {
    var title = ChildText(item, "title");
    var link = ChildText(item, "link");
    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
    {
      return null;
    }

    var description = ChildText(item, "description");
    var content = ChildText(item, "encoded");
    if (string.IsNullOrWhiteSpace(content))
    {
      content = description;
    }

    var thumbnail = item
      .Elements()
      .FirstOrDefault(element => element.Name.LocalName == "thumbnail")
      ?.Attribute("url")
      ?.Value;

    if (string.IsNullOrWhiteSpace(thumbnail))
    {
      thumbnail = ImageSourceRegex.Match(content).Groups[1].Value;
    }

    var rawDate = ChildText(item, "pubDate");
    var pubDate = DateTimeOffset.TryParse(
      rawDate,
      CultureInfo.InvariantCulture,
      DateTimeStyles.AssumeUniversal,
      out var parsedDate)
      ? parsedDate.ToString("O", CultureInfo.InvariantCulture)
      : rawDate;

    return new MediumFeedItem(title, link, content, description, thumbnail ?? string.Empty, pubDate);
  }

  private static string ChildText(XElement element, string localName)
  {
    return element
      .Elements()
      .FirstOrDefault(child => child.Name.LocalName == localName)
      ?.Value
      .Trim() ?? string.Empty;
  }

  private sealed record MediumFeedItem(
    string Title,
    string Link,
    string Content,
    string Description,
    string Thumbnail,
    string PubDate);
}
