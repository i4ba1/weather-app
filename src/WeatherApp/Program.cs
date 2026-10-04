using Microsoft.EntityFrameworkCore;
using WeatherApp.API;
using WeatherApp.Infrastructure;
using WeatherApp.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddWeatherApplication(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<WeatherDbContext>().Database.EnsureCreatedAsync();

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
    await Results.Problem(statusCode: context.HttpContext.Response.StatusCode).ExecuteAsync(context.HttpContext));
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapWeatherEndpoints();
app.MapFallback(async context =>
{
    var frontend = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
    if (context.Request.Path.StartsWithSegments("/api") || !HttpMethods.IsGet(context.Request.Method) || !File.Exists(frontend))
    {
        await Results.Problem(statusCode: 404, title: "Resource not found").ExecuteAsync(context);
        return;
    }
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(frontend);
});
app.Run();

public partial class Program;
