using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using UserManagement.Domain.Entities;
using UserManagement.Infrastructure.Persistence;
using UserManagement.Infrastructure.Services;
using Xunit;

namespace UserManagement.API.Tests;

// Regression coverage for a bug where JwtBearerOptions.MapInboundClaims defaulted to true, so the
// handler silently renamed the "sub" claim to a legacy long-form URI. Every authenticated endpoint
// that reads ClaimsPrincipal.FindFirstValue(JwtRegisteredClaimNames.Sub) then got null and crashed
// with ArgumentNullException from Guid.Parse — a 500 on every call, for any user, valid or not.
public class JwtClaimsMappingTests : IClassFixture<UserManagementWebApplicationFactory>
{
    private readonly UserManagementWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public JwtClaimsMappingTests(UserManagementWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string MintTokenFor(Guid userId, string userName)
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var tokenService = new JwtTokenService(config);
        return tokenService.GenerateToken(userId, userName);
    }

    [Fact]
    public async Task ValidateToken_WhenTokenIsValidAndUserExists_ResolvesSubClaimAndReturnsOk()
    {
        var user = User.Create(
            "jsmith", "jsmith@example.com", BCrypt.Net.BCrypt.HashPassword("Secret123!"),
            "Jane", "Smith", "555-0100", new DateOnly(1990, 1, 1));

        using IServiceScope seedScope = _factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<UserManagementDbContext>();
        seedDb.Users.Add(user);
        await seedDb.SaveChangesAsync();

        string token = MintTokenFor(user.Id, user.UserName);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.GetAsync("/auth/validate-token");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
