using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UserManagement.Application.Exceptions;
using Xunit;
using UserManagement.Application.Users.Commands.ChangePassword;
using UserManagement.Domain.Entities;
using UserManagement.Infrastructure.Persistence;

namespace UserManagement.Application.Tests.Users.Commands.ChangePassword;

public class ChangePasswordCommandHandlerTests
{
    private static UserManagementDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<UserManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenCurrentPasswordIsCorrect_SavesNewPasswordHash()
    {
        // Arrange
        await using UserManagementDbContext db = CreateDbContext();
        var user = User.Create("jdoe", "jdoe@example.com", BCrypt.Net.BCrypt.HashPassword("OldPass123!"), "John", "Doe", "555-0100", new DateOnly(1990, 1, 1));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new ChangePasswordCommandHandler(db);
        var command = new ChangePasswordCommand(user.Id, "OldPass123!", "NewPass456!");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        User? updated = await db.Users.FindAsync(user.Id);
        BCrypt.Net.BCrypt.Verify("NewPass456!", updated!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCurrentPasswordIsWrong_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using UserManagementDbContext db = CreateDbContext();
        var user = User.Create("jdoe", "jdoe@example.com", BCrypt.Net.BCrypt.HashPassword("OldPass123!"), "John", "Doe", "555-0100", new DateOnly(1990, 1, 1));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new ChangePasswordCommandHandler(db);
        var command = new ChangePasswordCommand(user.Id, "WrongPassword!", "NewPass456!");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        await using UserManagementDbContext db = CreateDbContext();
        var handler = new ChangePasswordCommandHandler(db);
        var command = new ChangePasswordCommand(Guid.NewGuid(), "OldPass123!", "NewPass456!");

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
