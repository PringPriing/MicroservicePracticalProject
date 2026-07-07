using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ProductCatalog.Domain.Entities;
using Xunit;

namespace ProductCatalog.API.Tests;

public class InventoryEndpointsTests : IClassFixture<ProductCatalogWebApplicationFactory>
{
    private readonly ProductCatalogWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InventoryEndpointsTests(ProductCatalogWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateInventory_WhenBodyIsMalformedJson_ReturnsBadRequestWithStandardErrorShape()
    {
        var content = new StringContent("not json at all", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PutAsync($"/inventory/{Guid.NewGuid()}", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement error = body.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        error.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateInventory_WhenNeitherDeltaNorSetQuantityProvided_ReturnsBadRequestValidationError()
    {
        HttpResponseMessage response = await _client.PutAsJsonAsync(
            $"/inventory/{Guid.NewGuid()}",
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task UpdateInventory_WithSetQuantity_UpdatesInventoryAndReturnsOk()
    {
        Category category = Category.Create("Widgets");
        var product = Product.Create("Widget", "desc", 9.99m, "USD", category.Id, [], new(), inventoryCount: 5);
        await using (var db = _factory.CreateDbContext())
        {
            db.Categories.Add(category);
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            $"/inventory/{product.Id}",
            new { setQuantity = 42 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("inventoryCount").GetInt32().Should().Be(42);
    }
}
