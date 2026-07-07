using MediatR;
using Microsoft.EntityFrameworkCore;
using UserManagement.Application.DTOs;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Users.Queries.GetUserById;

public sealed class GetUserByIdQueryHandler(DbContext db)
    : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken ct) =>
        await db.Set<User>()
            .AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(u => new UserDto(u.Id, u.UserName, u.FirstName, u.LastName, u.PhoneNumber, u.DateOfBirth))
            .FirstOrDefaultAsync(ct);
}
