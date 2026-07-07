using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UserManagement.Application.DTOs;
using UserManagement.Application.Services;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Users.Commands.LoginUser;

public record LoginUserCommand(string UserName, string Password) : IRequest<LoginResponseDto>;

public sealed class LoginUserCommandHandler(DbContext db, ITokenService tokenService)
    : IRequestHandler<LoginUserCommand, LoginResponseDto>
{
    // Verified even when no user is found, so a nonexistent username takes the same time as a wrong
    // password — otherwise short-circuiting past BCrypt lets an attacker enumerate usernames by timing.
    private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    public async Task<LoginResponseDto> Handle(LoginUserCommand request, CancellationToken ct)
    {
        var user = await db.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == request.UserName, ct);

        bool passwordIsValid = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyPasswordHash);

        if (user is null || !passwordIsValid)
            throw new UnauthorizedAccessException("Invalid credentials.");

        string token = tokenService.GenerateToken(user.Id, user.UserName);
        return new LoginResponseDto(token, user.Id, user.UserName);

    }
}

public sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty();
    }
}
