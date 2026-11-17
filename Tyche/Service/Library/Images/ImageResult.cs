namespace Tyche.Service.Images;

public sealed record ImageResult(ImageResultStatus Status, Stream? Content, string? ContentType, string? Message)
{
  public static ImageResult Ok(Stream content, string contentType) => new(ImageResultStatus.Ok, content, contentType, null);

  public static ImageResult NotFound(string message) => new(ImageResultStatus.NotFound, null, null, message);

  public static ImageResult Unavailable(string message) => new(ImageResultStatus.Unavailable, null, null, message);
}
