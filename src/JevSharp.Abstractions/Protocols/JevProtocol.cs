namespace JevSharp.Abstractions.Protocols;

/// <summary>The JSON protocol used by an endpoint.</summary>
public enum JevProtocol
{
    /// <summary>TypeSafe System One.</summary>
    TypeSafe,

    /// <summary>OpenRouter Decisions.</summary>
    OpenRouter,

    /// <summary>Vercel AI Gateway evaluation specification v4.</summary>
    Vercel
}
