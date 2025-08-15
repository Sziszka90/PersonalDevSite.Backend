using System;

namespace PersonalDevSite.Functions.Exceptions;

public class ChatGptRequestException : Exception
{
  public ChatGptRequestException() { }
  public ChatGptRequestException(string message) : base(message) { }
  public ChatGptRequestException(string message, Exception inner) : base(message, inner) { }
}

