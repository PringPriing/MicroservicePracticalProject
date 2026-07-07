using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Events;
using UserManagement.Application.Services;
using UserManagement.Application.Users.Commands.RegisterUser;
using UserManagement.Domain.Entities;
using UserManagement.Infrastructure.Persistence;
using Xunit;

namespace UserManagement.Application.Tests.Users.Commands.RegisterUser;

public class RegisterUserCommandHandlerTests
{
    private sealed class FakeEventBus : IEventBus
    {
        public List<(object Event, string RoutingKey)> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken ct) where TEvent : class
        {
            Published.Add((@event, routingKey));
            return Task.CompletedTask;
        }
    }

    private static UserManagementDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<UserManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RegisterUserCommand ValidCommand() => new(
        "jdoe", "jdoe@example.com", "Secret123!", "John", "Doe", "555-0100", new DateOnly(1990, 1, 1));

    [Fact]
    public async Task Handle_OnSuccess_PublishesUserRegisteredEvent()
    {
        await using UserManagementDbContext db = CreateDbContext();
        var eventBus = new FakeEventBus();
        var handler = new RegisterUserCommandHandler(db, eventBus);

        Guid userId = await handler.Handle(ValidCommand(), CancellationToken.None);

        eventBus.Published.Should().ContainSingle();
        (object publishedEvent, string routingKey) = eventBus.Published[0];
        routingKey.Should().Be("user.registered");
        var userRegistered = publishedEvent.Should().BeOfType<UserRegisteredEvent>().Subject;
        userRegistered.UserId.Should().Be(userId);
        userRegistered.UserName.Should().Be("jdoe");
        userRegistered.Email.Should().Be("jdoe@example.com");
    }

    [Fact]
    public async Task Handle_OnSuccess_PersistsUser()
    {
        await using UserManagementDbContext db = CreateDbContext();
        var eventBus = new FakeEventBus();
        var handler = new RegisterUserCommandHandler(db, eventBus);

        Guid userId = await handler.Handle(ValidCommand(), CancellationToken.None);

        (await db.Set<User>().FindAsync(userId)).Should().NotBeNull();
    }
}
