namespace JevSharp.Tests.Support;

/// <summary>Captured wire data that remains valid after HTTP messages are disposed.</summary>
internal sealed record RequestCapture(Uri? Uri, string Method, string Body, IReadOnlyDictionary<string, string> Headers);
