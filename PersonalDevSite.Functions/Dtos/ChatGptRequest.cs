using System.Collections.Generic;

namespace PersonalDevSite.Functions.Dtos;

public class ChatGptRequest
{
  public required string Model { get; set; }
  public required List<Message> Messages { get; set; }
}

