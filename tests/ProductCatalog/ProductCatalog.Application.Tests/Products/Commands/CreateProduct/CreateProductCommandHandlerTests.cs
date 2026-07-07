using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Products.Commands.CreateProduct;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;
using Xunit;

namespace ProductCatalog.Application.Tests.Products.Commands.CreateProduct;

public class CreateProductCommandHandlerTests
{
    private static ProductCatalogDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenCategoryExists_CreatesProductAndReturnsDto()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var category = Category.Create("Widgets");
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var handler = new CreateProductCommandHandler(db);
        var command = new CreateProductCommand(
            "Widget", "desc", 9.99m, "USD", category.Id, ["http://img"], new() { ["color"] = "red" }, 10);

        ProductDto result = await handler.Handle(command, CancellationToken.None);

        result.Name.Should().Be("Widget");
        result.CategoryId.Should().Be(category.Id);
        result.InventoryCount.Should().Be(10);
        (await db.Products.FindAsync(result.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenCategoryDoesNotExist_ThrowsNotFoundException()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var handler = new CreateProductCommandHandler(db);
        var command = new CreateProductCommand(
            "Widget", "desc", 9.99m, "USD", Guid.NewGuid(), [], new(), 10);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validate_WhenNameIsEmpty_ReturnsValidationError()
    {
        var validator = new CreateProductCommandValidator();
        var command = new CreateProductCommand("", "desc", 9.99m, "USD", Guid.NewGuid(), [], new(), 10);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateProductCommand.Name));
    }

    [Fact]
    public void Validate_WhenPriceIsNegative_ReturnsValidationError()
    {
        var validator = new CreateProductCommandValidator();
        var command = new CreateProductCommand("Widget", "desc", -1m, "USD", Guid.NewGuid(), [], new(), 10);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateProductCommand.Price));
    }

    [Fact]
    public void Validate_WhenCurrencyIsNotThreeLetters_ReturnsValidationError()
    {
        var validator = new CreateProductCommandValidator();
        var command = new CreateProductCommand("Widget", "desc", 9.99m, "US", Guid.NewGuid(), [], new(), 10);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateProductCommand.Currency));
    }
}
