using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tyche.Service.Processing;

namespace Tyche.Service.Test.Support;

public sealed class ImageApiFactory : WebApplicationFactory<Program>
{
  private readonly Dictionary<string, string?> settings = new();

  public ImageApiFactory(TempFolder temp, params (string Name, string Folder)[] collections)
  {
    CachePath = temp.Sub("cache");
    foreach (var (name, folder) in collections)
    {
      settings[$"ImageApi:Collections:{name}"] = folder;
    }

    settings["ImageApi:DefaultCollection"] = collections.Length > 0 ? collections[0].Name : "";
    settings["ImageApi:CachePath"] = CachePath;
    settings["ImageApi:ListingCacheSeconds"] = "1";
    settings["ImageApi:MaxConcurrentProcesses"] = "2";
  }

  public string CachePath { get; }

  public CountingProcessRunner Runner { get; } = new();

  public ImageApiFactory With(string key, string value)
  {
    settings[$"ImageApi:{key}"] = value;
    return this;
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
    builder.ConfigureServices(services =>
    {
      services.RemoveAll<IProcessRunner>();
      services.AddSingleton<IProcessRunner>(Runner);
    });
  }
}
