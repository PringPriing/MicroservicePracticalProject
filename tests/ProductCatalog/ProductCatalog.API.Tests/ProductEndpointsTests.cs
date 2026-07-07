using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ProductCatalog.Domain.Entities;
using Xunit;

namespace ProductCatalog.API.Tests;

public class ProductEndpointsTests : IClassFixture<ProductCatalogWebApplicationFactory>
{
    private readonly ProductCatalogWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductEndpointsTests(ProductCatalogWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProductById_WhenProductDoesNotExist_ReturnsNotFoundWithStandardErrorShape()
    {
        HttpResponseMessage response = await _client.GetAsync($"/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement error = body.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("NOT_FOUND");
        error.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ListProducts_WhenLimitExceeds100_IsCappedNotPassedThroughRaw()
    {
        Category category = Category.Create("Widgets");
        await using (var db = _factory.CreateDbContext())
        {
            db.Categories.Add(category);
            for (int i = 0; i < 150; i++)
                db.Products.Add(Product.Create($"Product {i:000}", "desc", 1m, "USD", category.Id, [], new(), 10));
            await db.SaveChangesAsync();
        }

        HttpResponseMessage response = await _client.GetAsync($"/products?category={category.Id}&limit=1000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("limit").GetInt32().Should().Be(100);
        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(100);
    }

    [Fact]
    public async Task CreateProduct_WhenCategoryDoesNotExist_ReturnsNotFoundWithStandardErrorShape()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/products", new
        {
            name = "Widget",
            description = "desc",
            price = 9.99m,
            currency = "USD",
            categoryId = Guid.NewGuid(),
            inventoryCount = 10
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task CreateProduct_WhenBodyIsMalformedJson_ReturnsBadRequestWithStandardErrorShape()
    {
        var content = new StringContent("{ this is not valid json", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync("/products", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement error = body.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        error.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }
}
