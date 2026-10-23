using System.Collections.Generic;

namespace PersonalDevSite.Functions.Dtos;

public class ChatGptResponse
{
  public required List<Choice> Choices { get; set; }
}

public class Choice
{
  public required Message Message { get; set; }
}
