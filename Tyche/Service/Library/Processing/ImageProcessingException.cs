namespace Tyche.Service.Processing;

/// <summary>ImageMagick failed, timed out or could not be started. The message is for the log only.</summary>
public sealed class ImageProcessingException : Exception
{
    public ImageProcessingException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
