using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ProductCatalog.Domain.Entities;
using Xunit;

namespace ProductCatalog.API.Tests;

public class CartEndpointsTests : IClassFixture<ProductCatalogWebApplicationFactory>
{
    private readonly ProductCatalogWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CartEndpointsTests(ProductCatalogWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<Product> SeedProductAsync(int inventoryCount)
    {
        Category category = Category.Create("Widgets");
        var product = Product.Create("Widget", "desc", 9.99m, "USD", category.Id, [], new(), inventoryCount);

        await using var db = _factory.CreateDbContext();
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product;
    }

    private async Task<Guid> SeedCartAsync()
    {
        Guid userId = Guid.NewGuid();
        await using var db = _factory.CreateDbContext();
        db.Set<Cart>().Add(Cart.Create(userId));
        await db.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public async Task AddCartItem_WhenInventoryIsZero_ReturnsConflictNotOk()
    {
        Product product = await SeedProductAsync(inventoryCount: 0);
        Guid userId = await SeedCartAsync();

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/cart/{userId}/items",
            new { productId = product.Id, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("CONFLICT");
    }

    [Fact]
    public async Task AddCartItem_WhenUserWasNeverRegistered_ReturnsNotFoundWithStandardErrorShape()
    {
        Product product = await SeedProductAsync(inventoryCount: 10);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/cart/{Guid.NewGuid()}/items",
            new { productId = product.Id, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task AddCartItem_WhenBodyIsMalformedJson_ReturnsBadRequestWithStandardErrorShape()
    {
        var content = new StringContent("{ this is not valid json", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync($"/cart/{Guid.NewGuid()}/items", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement error = body.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        error.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }
}
