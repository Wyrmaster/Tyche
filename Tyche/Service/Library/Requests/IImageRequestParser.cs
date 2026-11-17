namespace Tyche.Service.Requests;

public interface IImageRequestParser
{
  ImageRequestParseResult Parse(
    string? collection,
    string? width,
    string? height,
    string? dpi,
    string? mode,
    string? format);
}
