using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.Categories.Commands.CreateCategory;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;
using Xunit;

namespace ProductCatalog.Application.Tests.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandlerTests
{
    private static ProductCatalogDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenNoParent_CreatesCategoryAndReturnsDto()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var handler = new CreateCategoryCommandHandler(db);
        var command = new CreateCategoryCommand("Widgets", null);

        CategoryDto result = await handler.Handle(command, CancellationToken.None);

        result.Name.Should().Be("Widgets");
        result.ParentId.Should().BeNull();
        (await db.Categories.FindAsync(result.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenParentExists_CreatesCategoryUnderParent()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var parent = Category.Create("Electronics");
        db.Categories.Add(parent);
        await db.SaveChangesAsync();

        var handler = new CreateCategoryCommandHandler(db);
        var command = new CreateCategoryCommand("Widgets", parent.Id);

        CategoryDto result = await handler.Handle(command, CancellationToken.None);

        result.ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task Handle_WhenParentDoesNotExist_ThrowsNotFoundException()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var handler = new CreateCategoryCommandHandler(db);
        var command = new CreateCategoryCommand("Widgets", Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validate_WhenNameIsEmpty_ReturnsValidationError()
    {
        var validator = new CreateCategoryCommandValidator();
        var command = new CreateCategoryCommand("", null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateCategoryCommand.Name));
    }
}
