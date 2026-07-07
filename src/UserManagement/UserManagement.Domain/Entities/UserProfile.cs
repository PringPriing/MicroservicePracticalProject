namespace UserManagement.Domain.Entities;

public sealed class UserProfile
{
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private UserProfile() { }

    public void Update(string displayName, string? bio, string? avatarUrl)
    {
        DisplayName = displayName;
        Bio = bio;
        AvatarUrl = avatarUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public static UserProfile Create(Guid userId, string displayName) => new()
    {
        UserId = userId,
        DisplayName = displayName,
        UpdatedAt = DateTime.UtcNow
    };
}
