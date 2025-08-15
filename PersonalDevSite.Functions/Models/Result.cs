namespace PersonalDevSite.Functions.Models;

public class Result<T>
{
  public T? Data { get; set; }
  public string? Error { get; set; }
  public bool IsSuccess => Error == null;
}

