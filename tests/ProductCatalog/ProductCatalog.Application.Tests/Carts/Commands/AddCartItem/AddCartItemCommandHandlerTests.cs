using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.Carts.Commands.AddCartItem;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;
using Xunit;

namespace ProductCatalog.Application.Tests.Carts.Commands.AddCartItem;

public class AddCartItemCommandHandlerTests
{
    private static ProductCatalogDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenInventoryIsZero_ThrowsConflictException()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var product = Product.Create("Widget", "desc", 9.99m, "USD", Guid.NewGuid(), [], new(), inventoryCount: 0);
        db.Products.Add(product);
        Guid userId = Guid.NewGuid();
        db.Set<Cart>().Add(Cart.Create(userId));
        await db.SaveChangesAsync();

        var handler = new AddCartItemCommandHandler(db);
        var command = new AddCartItemCommand(userId, product.Id, 1);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenProductDoesNotExist_ThrowsNotFoundException()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        Guid userId = Guid.NewGuid();
        db.Set<Cart>().Add(Cart.Create(userId));
        await db.SaveChangesAsync();

        var handler = new AddCartItemCommandHandler(db);
        var command = new AddCartItemCommand(userId, Guid.NewGuid(), 1);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenCartDoesNotExist_ThrowsNotFoundException()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var product = Product.Create("Widget", "desc", 9.99m, "USD", Guid.NewGuid(), [], new(), inventoryCount: 10);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var handler = new AddCartItemCommandHandler(db);
        var command = new AddCartItemCommand(Guid.NewGuid(), product.Id, 1);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAccumulatedQuantityExceedsInventory_ThrowsConflictException()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var product = Product.Create("Widget", "desc", 9.99m, "USD", Guid.NewGuid(), [], new(), inventoryCount: 3);
        db.Products.Add(product);
        Guid userId = Guid.NewGuid();
        db.Set<Cart>().Add(Cart.Create(userId));
        await db.SaveChangesAsync();

        var handler = new AddCartItemCommandHandler(db);

        // First add brings the cart to the full available amount.
        await handler.Handle(new AddCartItemCommand(userId, product.Id, 3), CancellationToken.None);

        // A second add of just 1 more unit must be rejected, since 3 are already reserved in the cart.
        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new AddCartItemCommand(userId, product.Id, 1), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_OnSuccess_ReturnsCartSnapshotWithAccumulatedQuantity()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var product = Product.Create("Widget", "desc", 9.99m, "USD", Guid.NewGuid(), [], new(), inventoryCount: 10);
        db.Products.Add(product);
        Guid userId = Guid.NewGuid();
        db.Set<Cart>().Add(Cart.Create(userId));
        await db.SaveChangesAsync();

        var handler = new AddCartItemCommandHandler(db);

        await handler.Handle(new AddCartItemCommand(userId, product.Id, 2), CancellationToken.None);
        CartDto result = await handler.Handle(new AddCartItemCommand(userId, product.Id, 3), CancellationToken.None);

        result.UserId.Should().Be(userId);
        result.Items.Should().ContainSingle(i => i.ProductId == product.Id && i.Quantity == 5);
    }
}
