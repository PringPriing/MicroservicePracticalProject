using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Services;

namespace ProductCatalog.Infrastructure.Clients;

public sealed class UserManagementHttpClient(HttpClient httpClient, ILogger<UserManagementHttpClient> logger)
    : IUserManagementClient
{
    // UserManagement.API serializes with ASP.NET Core's Web JSON defaults (camelCase, case-insensitive) —
    // HttpClient's own ReadFromJsonAsync default is case-sensitive and would silently leave every
    // property at its default value otherwise.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UserProfileDto?> GetUserByIdAsync(Guid userId, string bearerToken, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/auth/{userId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<UserProfileDto>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // UserManagement.API being slow, down, or unreachable must not fail the cart write, which has
            // already committed by the time this is called — owner enrichment is best-effort only.
            logger.LogWarning(ex, "Failed to fetch user profile for {UserId} from UserManagement.API.", userId);
            return null;
        }
    }
}
