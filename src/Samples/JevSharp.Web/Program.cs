using JevSharp.Abstractions.Models;
using JevSharp.Core.Configuration;
using JevSharp.DependencyInjection;

namespace JevSharp.Samples.Web;

/// <summary>Configures and hosts the sample HTTP API.</summary>
internal static class Program
{
    /// <summary>Builds and starts the web application.</summary>
    /// <param name="args">Command-line arguments passed to the host.</param>
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        builder.Services.AddJev(options => options.UseOpenRouter(
            "your-openrouter-api-key",
            OpenRouterModels.Latest));

        var app = builder.Build();
        app.MapControllers();
        app.Run();
    }
}
