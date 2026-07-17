namespace VgmSharp;

public sealed class VgmStreamException : Exception
{
    public VgmStreamException(string message) : base(message) { }
    public VgmStreamException(string message, Exception inner) : base(message, inner) { }
}
