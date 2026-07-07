using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UserManagement.Application.Exceptions;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Users.Commands.UpdateUserProfile;

public record UpdateUserProfileCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateOnly DateOfBirth) : IRequest;

public sealed class UpdateUserProfileCommandHandler(DbContext db)
    : IRequestHandler<UpdateUserProfileCommand>
{
    public async Task Handle(UpdateUserProfileCommand request, CancellationToken ct)
    {
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException($"User {request.UserId} was not found.");

        user.UpdateProfile(request.FirstName, request.LastName, request.PhoneNumber, request.DateOfBirth);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow));
    }
}
