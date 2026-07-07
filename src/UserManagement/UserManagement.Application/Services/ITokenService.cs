namespace UserManagement.Application.Services;

public interface ITokenService
{
    string GenerateToken(Guid userId, string userName);
}
