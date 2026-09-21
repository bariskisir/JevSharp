namespace JevSharp.Tests.Live;

/// <summary>Skips live tests unless the explicit opt-in and relevant credential are present.</summary>
internal sealed class LiveFactAttribute : FactAttribute
{
    /// <summary>Checks names only and never displays credential values.</summary>
    public LiveFactAttribute(string credentialVariable)
    {
        if (Environment.GetEnvironmentVariable("JEV_RUN_LIVE_TESTS") != "1" || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(credentialVariable)))
        {
            Skip = $"Set JEV_RUN_LIVE_TESTS=1 and {credentialVariable} to enable this paid live test.";
        }
    }
}
