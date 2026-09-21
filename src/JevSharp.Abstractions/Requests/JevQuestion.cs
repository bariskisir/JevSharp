using JevSharp.Abstractions.Models;

namespace JevSharp.Abstractions.Requests;

/// <summary>A judgment about the shared state of an evaluation.</summary>
/// <param name="Instructions">Text, structured JSON, or an explicit null where supported.</param>
public abstract record JevQuestion(JevValue Instructions);
