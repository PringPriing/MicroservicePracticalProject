namespace Shared.Kernel.Exceptions;

public sealed class BadRequestException(string message) : Exception(message);
