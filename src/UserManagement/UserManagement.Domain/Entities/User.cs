namespace UserManagement.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsActive { get; private set; }

    private User() { }

    public void UpdateProfile(string firstName, string lastName, string phoneNumber, DateOnly dateOfBirth)
    {
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        DateOfBirth = dateOfBirth;
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
    }

    public static User Create(
        string userName,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string phoneNumber,
        DateOnly dateOfBirth)
    {
        return new User
        {
            Id = Guid.CreateVersion7(),
            UserName = userName,
            Email = email,
            PasswordHash = passwordHash,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            DateOfBirth = dateOfBirth,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }
}
