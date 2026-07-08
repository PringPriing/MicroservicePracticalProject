using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UserManagement.Application.DTOs;
using UserManagement.Application.Users.Queries.GetUserById;
using UserManagement.Domain.Entities;
using UserManagement.Infrastructure.Persistence;
using Xunit;

namespace UserManagement.Application.Tests.Users.Queries.GetUserById;

public class GetUserByIdQueryHandlerTests
{
    private static UserManagementDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<UserManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenUserExists_ReturnsUserDto()
    {
        // Arrange
        await using UserManagementDbContext db = CreateDbContext();
        var user = User.Create("jdoe", "jdoe@example.com", "hash", "John", "Doe", "555-0100", new DateOnly(1990, 1, 1));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new GetUserByIdQueryHandler(db);

        // Act
        UserDto? result = await handler.Handle(new GetUserByIdQuery(user.Id), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.UserName.Should().Be("jdoe");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using UserManagementDbContext db = CreateDbContext();
        var handler = new GetUserByIdQueryHandler(db);

        // Act
        UserDto? result = await handler.Handle(new GetUserByIdQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
