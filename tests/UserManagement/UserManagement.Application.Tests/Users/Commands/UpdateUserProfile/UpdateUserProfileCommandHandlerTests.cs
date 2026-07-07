using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UserManagement.Application.Exceptions;
using Xunit;
using UserManagement.Application.Users.Commands.UpdateUserProfile;
using UserManagement.Domain.Entities;
using UserManagement.Infrastructure.Persistence;

namespace UserManagement.Application.Tests.Users.Commands.UpdateUserProfile;

public class UpdateUserProfileCommandHandlerTests
{
    private static UserManagementDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<UserManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenUserExists_UpdatesProfileFields()
    {
        // Arrange
        await using UserManagementDbContext db = CreateDbContext();
        var user = User.Create("jdoe", "jdoe@example.com", BCrypt.Net.BCrypt.HashPassword("Secret123!"), "John", "Doe", "555-0100", new DateOnly(1990, 1, 1));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new UpdateUserProfileCommandHandler(db);
        var command = new UpdateUserProfileCommand(user.Id, "Jane", "Smith", "555-0199", new DateOnly(1992, 6, 15));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        User? updated = await db.Users.FindAsync(user.Id);
        updated!.FirstName.Should().Be("Jane");
        updated.LastName.Should().Be("Smith");
        updated.PhoneNumber.Should().Be("555-0199");
        updated.DateOfBirth.Should().Be(new DateOnly(1992, 6, 15));
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        await using UserManagementDbContext db = CreateDbContext();
        var handler = new UpdateUserProfileCommandHandler(db);
        var command = new UpdateUserProfileCommand(Guid.NewGuid(), "Jane", "Smith", "555-0199", new DateOnly(1992, 6, 15));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
