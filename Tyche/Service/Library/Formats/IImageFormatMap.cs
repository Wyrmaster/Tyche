namespace Tyche.Service.Formats;

public interface IImageFormatMap
{
    /// <summary>Maps a file extension (with or without dot, any case; jpeg is jpg) to a format.</summary>
    bool TryFromExtension(string extension, out ImageFormat format);

    /// <summary>Maps the value of the format query parameter (jpg, jpeg, png, webp, gif) to a format.</summary>
    bool TryParse(string name, out ImageFormat format);

    string GetContentType(ImageFormat format);

    /// <summary>The ImageMagick coder name used as file prefix, e.g. jpeg:.</summary>
    string GetMagickCoder(ImageFormat format);

    bool CanAnimate(ImageFormat format);

    IReadOnlyCollection<string> SupportedExtensions { get; }
}
