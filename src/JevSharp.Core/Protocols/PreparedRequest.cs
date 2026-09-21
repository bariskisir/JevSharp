using System.Text.Json;

namespace JevSharp.Core.Protocols;

/// <summary>A stable body and question snapshot shared by retries and response validation.</summary>
internal sealed record PreparedRequest(string Model, byte[] Body, JsonElement Questions);
