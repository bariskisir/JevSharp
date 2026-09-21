namespace JevSharp.Tests.Support;

/// <summary>Exercises the shared transport with every built-in provider and a custom endpoint.</summary>
internal static class ProviderFixture
{
    internal static readonly string[] Names = ["TypeSafe", "OpenRouter", "Vercel", "Custom"];

    /// <summary>Configures a provider using dummy credentials and a single attempt.</summary>
    internal static void Configure(JevClientOptions options, string provider)
    {
        switch (provider)
        {
            case "TypeSafe":
                options.UseTypeSafe("test-key");
                break;
            case "OpenRouter":
                options.UseOpenRouter("test-key");
                break;
            case "Vercel":
                options.UseVercel("test-key");
                break;
            case "Custom":
                options.UseCustom(new("https://custom.example/evaluate"), JevProtocol.TypeSafe, "custom-model",
                    new Dictionary<string, string> { ["X-Api-Key"] = "test-key" });
                break;
            default:
                throw new ArgumentException("Unknown test provider.", nameof(provider));
        }

        options.Retry.MaxAttempts = 1;
    }

    /// <summary>Returns the provider's wire representation of the same answer.</summary>
    internal static string Response(string provider) => provider == "Vercel"
        ? """{"answers":{"yes":{"type":"boolean","probability":0.9}}}"""
        : Fixtures.Single;
}
