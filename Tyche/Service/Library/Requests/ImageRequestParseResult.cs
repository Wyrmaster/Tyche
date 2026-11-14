namespace Tyche.Service.Requests;

public sealed record ImageRequestParseResult(ImageRequest? Request, IReadOnlyDictionary<string, string[]> Problems)
{
    public bool IsValid => Request is not null;
}
