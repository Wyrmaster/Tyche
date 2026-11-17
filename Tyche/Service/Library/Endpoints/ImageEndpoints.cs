using Tyche.Service.Images;
using Tyche.Service.Processing;
using Tyche.Service.Requests;
using Tyche.Service.Versioning;

namespace Tyche.Service.Endpoints;

public static class ImageEndpoints
{
  public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/image", GetImageAsync);
    routes.MapGet("/version", (IVersionProvider version) => Results.Text(version.Version));
    return routes;
  }

  private static async Task<IResult> GetImageAsync(
    HttpContext context,
    IImageRequestParser parser,
    IImageService service,
    ILogger<ImageService> logger,
    string? collection,
    string? width,
    string? height,
    string? dpi,
    string? mode,
    string? format)
  {
    context.Response.Headers.CacheControl = "no-store";

    var parsed = parser.Parse(collection, width, height, dpi, mode, format);
    if (!parsed.IsValid)
    {
      return Results.ValidationProblem(
        parsed.Problems,
        detail: "One or more request parameters are invalid.",
        statusCode: StatusCodes.Status400BadRequest);
    }

    try
    {
      var result = await service.GetRandomImageAsync(parsed.Request!, context.RequestAborted);
      return result.Status switch
      {
        ImageResultStatus.Ok => Results.Stream(result.Content!, result.ContentType),
        ImageResultStatus.Unavailable => Results.Problem(
          result.Message,
          statusCode: StatusCodes.Status503ServiceUnavailable,
          title: "Temporarily unavailable"),
        _ => Results.Problem(result.Message, statusCode: StatusCodes.Status404NotFound, title: "Not found"),
      };
    }
    catch (ImageProcessingException exception)
    {
      logger.LogError(exception, "Image processing failed");
      return Results.Problem(
        "The image could not be processed.",
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Image processing failed");
    }
  }
}