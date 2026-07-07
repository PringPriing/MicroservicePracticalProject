using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ProductCatalog.Infrastructure.Persistence;

namespace ProductCatalog.API.Tests;

// Boots the real ProductCatalog.API host in-process, swapping SQL Server for a shared-cache SQLite
// in-memory database so tests exercise the actual endpoints, middleware, and error-shape mapping
// without requiring a real SQL Server instance.
public sealed class ProductCatalogWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _keepAliveConnection;
    private readonly string _connectionString;

    public ProductCatalogWebApplicationFactory()
    {
        _connectionString = $"Data Source=file:{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // AddDbContext composes options from every registered IDbContextOptionsConfiguration<T> —
            // removing only DbContextOptions<T> leaves Program.cs's UseSqlServer(...) configuration in
            // place, so it gets layered together with UseSqlite(...) below and EF Core rejects having
            // two relational providers on the same options instance. Both registrations must go.
            services.RemoveAll<DbContextOptions<ProductCatalogDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ProductCatalogDbContext>>();
            services.AddDbContext<ProductCatalogDbContext>(options => options.UseSqlite(_connectionString));

            // No real RabbitMQ broker is available in this test environment — UserRegisteredConsumer
            // is currently the only IHostedService, so removing all of them keeps it from trying to
            // open a real AMQP connection on host startup.
            services.RemoveAll<IHostedService>();

            using IServiceScope scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();
            db.Database.EnsureCreated();
        });
    }

    public ProductCatalogDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ProductCatalogDbContext>().UseSqlite(_connectionString).Options);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _keepAliveConnection.Dispose();
    }
}
