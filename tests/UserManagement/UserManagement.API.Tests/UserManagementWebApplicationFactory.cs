using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UserManagement.Application.Services;
using UserManagement.Infrastructure.Persistence;

namespace UserManagement.API.Tests;

// No real RabbitMQ broker is available in this test environment; without swapping this out,
// POST /auth/register would try to open a real AMQP connection via the singleton RabbitMqEventBus.
internal sealed class NoOpEventBus : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken ct) where TEvent : class =>
        Task.CompletedTask;
}

// Boots the real UserManagement.API host in-process, swapping SQL Server for a shared-cache SQLite
// in-memory database so tests exercise the actual endpoints and exception-handler middleware without
// requiring a real SQL Server instance.
public sealed class UserManagementWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _keepAliveConnection;
    private readonly string _connectionString;

    public UserManagementWebApplicationFactory()
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
            services.RemoveAll<DbContextOptions<UserManagementDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<UserManagementDbContext>>();
            services.AddDbContext<UserManagementDbContext>(options => options.UseSqlite(_connectionString));

            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus, NoOpEventBus>();

            using IServiceScope scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<UserManagementDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _keepAliveConnection.Dispose();
    }
}
