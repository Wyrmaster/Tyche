using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Integration;

public abstract class IntegrationTestBase : IDisposable
{
  private ImageApiFactory? factory;
  private HttpClient? client;

  protected TempFolder Temp { get; } = new();

  protected string Nature => Temp.Sub("nature");

  protected ImageApiFactory Factory => factory ?? throw new InvalidOperationException("Call Start first.");

  protected HttpClient Client => client ?? throw new InvalidOperationException("Call Start first.");

  protected ImageApiFactory Prepare(params (string Name, string Folder)[] collections)
    => factory = new ImageApiFactory(Temp, collections);

  protected HttpClient Start(params (string Name, string Folder)[] collections)
  {
    var created = factory ?? Prepare(collections.Length == 0 ? [("nature", Nature)] : collections);
    return client = created.CreateClient();
  }

  protected async Task<(HttpResponseMessage Response, byte[] Body)> GetAsync(string url)
  {
    var response = await Client.GetAsync(url);
    return (response, await response.Content.ReadAsByteArrayAsync());
  }

  public void Dispose()
  {
    client?.Dispose();
    factory?.Dispose();
    Temp.Dispose();
  }
}
