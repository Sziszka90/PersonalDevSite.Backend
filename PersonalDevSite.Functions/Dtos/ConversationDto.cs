using System.Collections.Generic;

namespace PersonalDevSite.Functions.Dtos;

public class ConversationDto
{
  public string Message { get; set; } = string.Empty;
  public List<Message> History { get; set; } = new();
}
