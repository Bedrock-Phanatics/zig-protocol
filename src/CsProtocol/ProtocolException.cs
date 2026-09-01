using System;

namespace CsProtocol;

/// <summary>Thrown when a Bedrock payload is malformed or violates configured resource limits.</summary>
public sealed class ProtocolException : Exception
{
    public ProtocolException(string message) : base(message) { }
    public ProtocolException(string message, Exception innerException) : base(message, innerException) { }
}
