using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Products.Queries.ListProducts;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Infrastructure.Persistence;
using Xunit;

namespace ProductCatalog.Application.Tests.Products.Queries.ListProducts;

public class ListProductsQueryHandlerTests
{
    private static ProductCatalogDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenRequestedLimitExceedsMax_CapsAt100()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        Guid categoryId = Guid.NewGuid();
        for (int i = 0; i < 150; i++)
            db.Products.Add(Product.Create($"Product {i:000}", "desc", 1m, "USD", categoryId, [], new(), 10));
        await db.SaveChangesAsync();

        var handler = new ListProductsQueryHandler(db);

        PagedResult<ProductDto> result = await handler.Handle(new ListProductsQuery(null, 1, 1000, null), CancellationToken.None);

        result.Limit.Should().Be(100);
        result.Items.Should().HaveCount(100);
    }

    [Fact]
    public async Task Handle_FiltersByCategory()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        Guid categoryA = Guid.NewGuid();
        Guid categoryB = Guid.NewGuid();
        db.Products.Add(Product.Create("In A", "desc", 1m, "USD", categoryA, [], new(), 10));
        db.Products.Add(Product.Create("In B", "desc", 1m, "USD", categoryB, [], new(), 10));
        await db.SaveChangesAsync();

        var handler = new ListProductsQueryHandler(db);

        PagedResult<ProductDto> result = await handler.Handle(new ListProductsQuery(categoryA, 1, 20, null), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Name.Should().Be("In A");
    }

    [Fact]
    public async Task Handle_WhenSortIsPrice_OrdersAscendingByPrice()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        Guid categoryId = Guid.NewGuid();
        db.Products.Add(Product.Create("Expensive", "desc", 100m, "USD", categoryId, [], new(), 10));
        db.Products.Add(Product.Create("Cheap", "desc", 5m, "USD", categoryId, [], new(), 10));
        await db.SaveChangesAsync();

        var handler = new ListProductsQueryHandler(db);

        PagedResult<ProductDto> result = await handler.Handle(new ListProductsQuery(categoryId, 1, 20, "price"), CancellationToken.None);

        result.Items.Select(p => p.Name).Should().ContainInOrder("Cheap", "Expensive");
    }

    [Fact]
    public void Validate_WhenSortIsPopularity_ReturnsValidationError()
    {
        var validator = new ListProductsQueryValidator();
        var query = new ListProductsQuery(null, 1, 20, "popularity");

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ListProductsQuery.Sort));
    }

    [Fact]
    public void Validate_WhenPageIsLessThanOne_ReturnsValidationError()
    {
        var validator = new ListProductsQueryValidator();
        var query = new ListProductsQuery(null, 0, 20, null);

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ListProductsQuery.Page));
    }
}
