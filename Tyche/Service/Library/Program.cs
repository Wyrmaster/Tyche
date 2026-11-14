using Tyche.Service;
using Tyche.Service.Caching;
using Tyche.Service.Endpoints;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddImageApi(builder.Configuration);

WebApplication app = builder.Build();

// Create the cache folder and remove leftovers of a crash right at startup.
app.Services.GetRequiredService<IImageCache>();

app.MapImageEndpoints();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
