
using System;

namespace PersonalDevSite.Functions.Models;

public class ChunkEmbedding
{
  public string Text { get; set; } = string.Empty;
  public float[] Embedding { get; set; } = Array.Empty<float>();
}
