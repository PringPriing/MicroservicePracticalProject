namespace Shared.Kernel.Events;

public sealed record UserRegisteredEvent(Guid UserId, string UserName, string Email, DateTime RegisteredAt);
