using ProductCatalog.Application.DTOs;

namespace ProductCatalog.Application.Services;

public interface IUserManagementClient
{
    Task<UserProfileDto?> GetUserByIdAsync(Guid userId, string bearerToken, CancellationToken ct);
}
