using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Integration;

public abstract class IntegrationTestBase : IDisposable
{
  private ImageApiFactory? _factory;
  private HttpClient? _client;

  protected TempFolder Temp { get; } = new();

  protected string Nature => Temp.Sub("nature");

  protected ImageApiFactory Factory => _factory ?? throw new InvalidOperationException("Call Start first.");

  protected HttpClient Client => _client ?? throw new InvalidOperationException("Call Start first.");

  protected ImageApiFactory Prepare(params (string Name, string Folder)[] collections)
    => _factory = new ImageApiFactory(Temp, collections);

  protected HttpClient Start(params (string Name, string Folder)[] collections)
  {
    var created = _factory ?? Prepare(collections.Length == 0 ? [("nature", Nature)] : collections);
    return _client = created.CreateClient();
  }

  protected async Task<(HttpResponseMessage Response, byte[] Body)> GetAsync(string url)
  {
    var response = await Client.GetAsync(url);
    return (response, await response.Content.ReadAsByteArrayAsync());
  }

  public void Dispose()
  {
    _client?.Dispose();
    _factory?.Dispose();
    Temp.Dispose();
  }
}
