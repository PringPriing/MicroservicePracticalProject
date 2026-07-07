using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Products.Queries.GetProductById;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Infrastructure.Persistence;
using Xunit;

namespace ProductCatalog.Application.Tests.Products.Queries.GetProductById;

public class GetProductByIdQueryHandlerTests
{
    private static ProductCatalogDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenProductExists_ReturnsProductDto()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var product = Product.Create("Widget", "A widget", 9.99m, "USD", Guid.NewGuid(), ["https://img/1.png"], new Dictionary<string, string> { ["color"] = "red" }, 5);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var handler = new GetProductByIdQueryHandler(db);

        ProductDto? result = await handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Name.Should().Be("Widget");
        result.InventoryCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WhenProductDoesNotExist_ReturnsNull()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var handler = new GetProductByIdQueryHandler(db);

        ProductDto? result = await handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }
}
