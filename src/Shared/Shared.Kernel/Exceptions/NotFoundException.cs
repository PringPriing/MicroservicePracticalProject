namespace Shared.Kernel.Exceptions;

public sealed class NotFoundException(string message) : Exception(message);
