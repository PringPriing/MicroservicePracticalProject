using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UserManagement.Application.DTOs;
using UserManagement.Application.Services;
using UserManagement.Application.Users.Commands.LoginUser;
using UserManagement.Domain.Entities;
using UserManagement.Infrastructure.Persistence;
using Xunit;

namespace UserManagement.Application.Tests.Users.Commands.LoginUser;

public class LoginUserCommandHandlerTests
{
    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateToken(Guid userId, string userName) => "fake-token";
    }

    private static UserManagementDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<UserManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenCredentialsAreCorrect_ReturnsLoginResponse()
    {
        // Arrange
        await using UserManagementDbContext db = CreateDbContext();
        var user = User.Create("jdoe", "jdoe@example.com", BCrypt.Net.BCrypt.HashPassword("Secret123!"), "John", "Doe", "555-0100", new DateOnly(1990, 1, 1));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new LoginUserCommandHandler(db, new FakeTokenService());
        var command = new LoginUserCommand("jdoe", "Secret123!");

        // Act
        LoginResponseDto response = await handler.Handle(command, CancellationToken.None);

        // Assert
        response.Token.Should().Be("fake-token");
        response.UserId.Should().Be(user.Id);
        response.UserName.Should().Be("jdoe");
    }

    [Fact]
    public async Task Handle_WhenPasswordIsWrong_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using UserManagementDbContext db = CreateDbContext();
        var user = User.Create("jdoe", "jdoe@example.com", BCrypt.Net.BCrypt.HashPassword("Secret123!"), "John", "Doe", "555-0100", new DateOnly(1990, 1, 1));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new LoginUserCommandHandler(db, new FakeTokenService());
        var command = new LoginUserCommand("jdoe", "WrongPassword!");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        await using UserManagementDbContext db = CreateDbContext();
        var handler = new LoginUserCommandHandler(db, new FakeTokenService());
        var command = new LoginUserCommand("ghost", "Secret123!");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void Validate_WhenUserNameIsEmpty_ReturnsValidationError()
    {
        // Arrange
        var validator = new LoginUserCommandValidator();
        var command = new LoginUserCommand("", "Secret123!");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(LoginUserCommand.UserName));
    }
}
