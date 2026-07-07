using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Inventory.Commands.UpdateInventory;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;
using Xunit;

namespace ProductCatalog.Application.Tests.Inventory.Commands.UpdateInventory;

// EF Core's InMemory provider doesn't support ExecuteUpdateAsync (a relational-only feature) and has no
// real locking, so it can't exercise the atomic-update guard these tests are here to verify. A SQLite
// shared-cache in-memory database gives every test its own independent connections while still behaving
// like a real relational database — including serializing concurrent writers, which the concurrency test needs.
public class UpdateInventoryCommandHandlerTests
{
    private static async Task<(string ConnectionString, SqliteConnection KeepAlive)> CreateSharedDatabaseAsync()
    {
        string connectionString = $"Data Source=file:{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        await using ProductCatalogDbContext db = CreateContext(connectionString);
        await db.Database.EnsureCreatedAsync();

        return (connectionString, keepAlive);
    }

    private static ProductCatalogDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ProductCatalogDbContext>().UseSqlite(connectionString).Options);

    private static async Task<Product> SeedProductAsync(string connectionString, int inventoryCount)
    {
        var category = Category.Create("Test Category");
        var product = Product.Create("Widget", "desc", 9.99m, "USD", category.Id, [], new(), inventoryCount);

        await using ProductCatalogDbContext seedDb = CreateContext(connectionString);
        seedDb.Categories.Add(category);
        seedDb.Products.Add(product);
        await seedDb.SaveChangesAsync();

        return product;
    }

    [Fact]
    public async Task Handle_WithSetQuantity_SetsAbsoluteInventoryCount()
    {
        (string connectionString, SqliteConnection keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;

        Product product = await SeedProductAsync(connectionString, inventoryCount: 5);

        await using ProductCatalogDbContext db = CreateContext(connectionString);
        var handler = new UpdateInventoryCommandHandler(db);

        InventoryDto result = await handler.Handle(new UpdateInventoryCommand(product.Id, null, 42), CancellationToken.None);

        result.InventoryCount.Should().Be(42);
    }

    [Fact]
    public async Task Handle_WithPositiveDelta_IncreasesInventoryCount()
    {
        (string connectionString, SqliteConnection keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;

        Product product = await SeedProductAsync(connectionString, inventoryCount: 5);

        await using ProductCatalogDbContext db = CreateContext(connectionString);
        var handler = new UpdateInventoryCommandHandler(db);

        InventoryDto result = await handler.Handle(new UpdateInventoryCommand(product.Id, 10, null), CancellationToken.None);

        result.InventoryCount.Should().Be(15);
    }

    [Fact]
    public async Task Handle_WithDeltaThatWouldGoNegative_ThrowsConflictAndLeavesCountUnchanged()
    {
        (string connectionString, SqliteConnection keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;

        Product product = await SeedProductAsync(connectionString, inventoryCount: 3);

        await using ProductCatalogDbContext db = CreateContext(connectionString);
        var handler = new UpdateInventoryCommandHandler(db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new UpdateInventoryCommand(product.Id, -5, null), CancellationToken.None));

        await using ProductCatalogDbContext verifyDb = CreateContext(connectionString);
        Product unchanged = await verifyDb.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);
        unchanged.InventoryCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WhenProductDoesNotExist_ThrowsNotFoundException()
    {
        (string connectionString, SqliteConnection keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;

        await using ProductCatalogDbContext db = CreateContext(connectionString);
        var handler = new UpdateInventoryCommandHandler(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdateInventoryCommand(Guid.NewGuid(), -1, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithConcurrentDecrements_NeverProducesNegativeInventory()
    {
        (string connectionString, SqliteConnection keepAlive) = await CreateSharedDatabaseAsync();
        await using var _ = keepAlive;

        const int initialInventory = 5;
        const int concurrentRequests = 10;

        Product product = await SeedProductAsync(connectionString, inventoryCount: initialInventory);

        IEnumerable<Task<bool>> attempts = Enumerable.Range(0, concurrentRequests).Select(async _ =>
        {
            await using ProductCatalogDbContext db = CreateContext(connectionString);
            var handler = new UpdateInventoryCommandHandler(db);
            try
            {
                await handler.Handle(new UpdateInventoryCommand(product.Id, -1, null), CancellationToken.None);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        });

        bool[] outcomes = await Task.WhenAll(attempts);

        await using ProductCatalogDbContext verifyDb = CreateContext(connectionString);
        Product final = await verifyDb.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);

        final.InventoryCount.Should().Be(0);
        final.InventoryCount.Should().BeGreaterThanOrEqualTo(0);
        outcomes.Count(succeeded => succeeded).Should().Be(initialInventory);
    }
}
