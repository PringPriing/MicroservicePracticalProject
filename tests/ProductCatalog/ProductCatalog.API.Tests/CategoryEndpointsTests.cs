using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ProductCatalog.API.Tests;

public class CategoryEndpointsTests : IClassFixture<ProductCatalogWebApplicationFactory>
{
    private readonly ProductCatalogWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CategoryEndpointsTests(ProductCatalogWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private void Authenticate() =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.MintToken(_factory.Services.GetRequiredService<IConfiguration>(), Guid.NewGuid()));

    [Fact]
    public async Task CreateCategory_WhenParentDoesNotExist_ReturnsNotFoundWithStandardErrorShape()
    {
        Authenticate();
        HttpResponseMessage response = await _client.PostAsJsonAsync("/categories", new
        {
            name = "Widgets",
            parentId = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task CreateCategory_WhenBodyIsMalformedJson_ReturnsBadRequestWithStandardErrorShape()
    {
        Authenticate();
        var content = new StringContent("{ this is not valid json", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync("/categories", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement error = body.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        error.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateCategory_WhenNoBearerToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/categories", new { name = "Widgets" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
