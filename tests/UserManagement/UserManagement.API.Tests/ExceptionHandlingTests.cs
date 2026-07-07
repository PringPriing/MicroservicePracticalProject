using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using UserManagement.Infrastructure.Services;
using Xunit;

namespace UserManagement.API.Tests;

// Regression coverage for a bug where any exception not explicitly matched in Program.cs's
// UseExceptionHandler switch (e.g. NotFoundException, or a duplicate-registration DbUpdateException)
// fell through with no case and no default, leaving the response as an empty 500 with no body.
public class ExceptionHandlingTests : IClassFixture<UserManagementWebApplicationFactory>
{
    private readonly UserManagementWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ExceptionHandlingTests(UserManagementWebApplicationFactory factory)
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
    public async Task ChangePassword_WhenUserDoesNotExist_ReturnsNotFoundWithBody()
    {
        string token = MintTokenFor(Guid.NewGuid(), "ghost");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.PatchAsJsonAsync(
            "/auth/password",
            new { currentPassword = "OldPass123!", newPassword = "NewPass456!" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_ReturnsNonEmptyErrorResponse_NotBlank500()
    {
        var request = new
        {
            userName = $"user-{Guid.NewGuid():N}",
            email = "duplicate@example.com",
            password = "Secret123!",
            firstName = "John",
            lastName = "Doe",
            phoneNumber = "555-0100",
            dateOfBirth = "1990-01-01"
        };

        HttpResponseMessage first = await _client.PostAsJsonAsync("/auth/register", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = request with { userName = $"user-{Guid.NewGuid():N}" };
        HttpResponseMessage second = await _client.PostAsJsonAsync("/auth/register", duplicate);

        // Previously this fell through the exception-handler switch with no matching case and no
        // default, leaving a blank body. It's not mapped to a specific status like 409 here — the
        // fix under test is that unmatched exceptions always get a real, non-empty JSON response.
        string body = await second.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
        second.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
