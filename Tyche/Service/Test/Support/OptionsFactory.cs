using Microsoft.Extensions.Options;
using Tyche.Service.Configuration;

namespace Tyche.Service.Test.Support;

public static class OptionsFactory
{
  public static IOptions<ImageApiOptions> Create(Action<ImageApiOptions>? configure = null)
  {
    var options = new ImageApiOptions();
    configure?.Invoke(options);
    return Options.Create(options);
  }
}
