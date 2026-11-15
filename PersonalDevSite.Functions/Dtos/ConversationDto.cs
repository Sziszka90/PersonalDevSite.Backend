using System.Collections.Generic;

namespace PersonalDevSite.Functions.Dtos;

public class ConversationDto
{
  public string Message { get; set; } = string.Empty;
  public string? PreviousResponseId { get; set; }
  public string? ResponseId { get; set; }
}
