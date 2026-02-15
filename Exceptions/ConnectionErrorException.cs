using System;

namespace SaperMultiplayer.Exceptions;

internal class ConnectionErrorException : Exception
{
    public ConnectionErrorException() { }

    public ConnectionErrorException(string message) : base(message) { }

    public ConnectionErrorException(string message, Exception inner) : base(message, inner) { }
}
