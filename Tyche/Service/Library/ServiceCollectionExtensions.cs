using System.Reflection;
using Microsoft.Extensions.Options;
using Tyche.Service.Caching;
using Tyche.Service.Catalog;
using Tyche.Service.Configuration;
using Tyche.Service.Formats;
using Tyche.Service.Health;
using Tyche.Service.Images;
using Tyche.Service.Processing;
using Tyche.Service.Requests;
using Tyche.Service.Versioning;

namespace Tyche.Service;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddImageApi(this IServiceCollection services, IConfiguration configuration)
  {
    services
      .AddOptions<ImageApiOptions>()
      .Bind(configuration.GetSection(ImageApiOptions.SectionName))
      .ValidateOnStart();
    services.AddSingleton<IValidateOptions<ImageApiOptions>, ImageApiOptionsValidator>();

    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IImageFormatMap, ImageFormatMap>();
    services.AddSingleton<ICollectionCatalog, CollectionCatalog>();
    services.AddSingleton<IRandomImagePicker, RandomImagePicker>();
    services.AddSingleton<IImageRequestParser, ImageRequestParser>();
    services.AddSingleton<IProcessRunner, ProcessRunner>();
    services.AddSingleton<IMagickArgumentBuilder, MagickArgumentBuilder>();
    services.AddSingleton<IImageProcessor, ImageProcessor>();
    services.AddSingleton<IImageCache, DiskImageCache>();
    services.AddSingleton<IImageService, ImageService>();
    services.AddSingleton<IVersionProvider>(_ => new VersionProvider(
      typeof(ServiceCollectionExtensions)
        .Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion));

    services.AddHealthChecks().AddCheck<MagickHealthCheck>("imagemagick");
    return services;
  }
}
