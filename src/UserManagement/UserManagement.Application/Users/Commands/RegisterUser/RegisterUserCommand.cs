using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Events;
using UserManagement.Application.Services;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Users.Commands.RegisterUser;

public record RegisterUserCommand(
    string UserName,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateOnly DateOfBirth) : IRequest<Guid>;

public sealed class RegisterUserCommandHandler(DbContext db, IEventBus eventBus)
    : IRequestHandler<RegisterUserCommand, Guid>
{
    public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = User.Create(
            request.UserName,
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.DateOfBirth);

        db.Set<User>().Add(user);
        await db.SaveChangesAsync(ct);

        await eventBus.PublishAsync(
            new UserRegisteredEvent(user.Id, user.UserName, user.Email, user.CreatedAt),
            "user.registered",
            ct);

        return user.Id;
    }
}

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow));
    }
}
