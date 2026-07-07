namespace Shared.Kernel;

public sealed record ErrorResponse(ErrorDetail Error);

public sealed record ErrorDetail(string Code, string Message);
