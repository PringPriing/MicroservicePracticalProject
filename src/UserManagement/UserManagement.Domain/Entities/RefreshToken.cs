namespace UserManagement.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }

    private RefreshToken() { }

    public void Revoke() => IsRevoked = true;

    public static RefreshToken Create(Guid userId, string token, DateTime expiresAt) => new()
    {
        Id = Guid.CreateVersion7(),
        Token = token,
        UserId = userId,
        ExpiresAt = expiresAt,
        IsRevoked = false
    };
}
