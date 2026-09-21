namespace JevSharp.Core.Protocols;

/// <summary>Reusable, thread-safe implementations of the built-in JSON formats.</summary>
public static class JevProtocols
{
    /// <summary>Gets the TypeSafe System One format.</summary>
    public static IJevProtocol TypeSafe { get; } = new BuiltInProtocol(JevProtocol.TypeSafe);
    /// <summary>Gets the OpenRouter Decisions format.</summary>
    public static IJevProtocol OpenRouter { get; } = new BuiltInProtocol(JevProtocol.OpenRouter);
    /// <summary>Gets the Vercel AI Gateway evaluation format.</summary>
    public static IJevProtocol Vercel { get; } = new BuiltInProtocol(JevProtocol.Vercel);

    /// <summary>Resolves a built-in format selected through the JevProtocol enum.</summary>
    internal static IJevProtocol ResolveBuiltIn(JevProtocol protocol) => protocol switch
    {
        JevProtocol.TypeSafe => TypeSafe,
        JevProtocol.OpenRouter => OpenRouter,
        JevProtocol.Vercel => Vercel,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol))
    };

}
