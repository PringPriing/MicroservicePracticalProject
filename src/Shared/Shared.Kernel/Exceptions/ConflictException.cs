namespace Shared.Kernel.Exceptions;

public sealed class ConflictException(string message) : Exception(message);
