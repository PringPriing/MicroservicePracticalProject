using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.Carts.Commands.CreateCart;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Infrastructure.Persistence;
using Xunit;

namespace ProductCatalog.Application.Tests.Carts.Commands.CreateCart;

public class CreateCartCommandHandlerTests
{
    private static ProductCatalogDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenNoCartExists_CreatesEmptyCart()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        var handler = new CreateCartCommandHandler(db);
        Guid userId = Guid.NewGuid();

        await handler.Handle(new CreateCartCommand(userId), CancellationToken.None);

        Cart? cart = await db.Set<Cart>().FirstOrDefaultAsync(c => c.UserId == userId);
        cart.Should().NotBeNull();
        cart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenCartAlreadyExists_IsNoOp()
    {
        await using ProductCatalogDbContext db = CreateDbContext();
        Guid userId = Guid.NewGuid();
        db.Set<Cart>().Add(Cart.Create(userId));
        await db.SaveChangesAsync();

        var handler = new CreateCartCommandHandler(db);
        await handler.Handle(new CreateCartCommand(userId), CancellationToken.None);

        (await db.Set<Cart>().CountAsync(c => c.UserId == userId)).Should().Be(1);
    }
}
