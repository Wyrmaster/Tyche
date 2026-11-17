using Tyche.Service.Formats;

namespace Tyche.Service.Requests;

/// <summary>A validated request. Width and height are already scaled by dpi (output pixels).</summary>
public sealed record ImageRequest(
  string? Collection,
  int? Width,
  int? Height,
  int Dpi,
  ResizeMode Mode,
  ImageFormat? Format)
{
  /// <summary>True when the original file is served unchanged.</summary>
  public bool IsPassThrough => Width is null && Height is null && Format is null;
}
