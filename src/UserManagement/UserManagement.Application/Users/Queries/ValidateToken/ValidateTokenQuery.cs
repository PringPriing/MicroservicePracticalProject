using MediatR;
using Microsoft.EntityFrameworkCore;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Users.Queries.ValidateToken;

public record ValidateTokenQuery(Guid UserId) : IRequest<bool>;

public sealed class ValidateTokenQueryHandler(DbContext db)
    : IRequestHandler<ValidateTokenQuery, bool>
{
    public async Task<bool> Handle(ValidateTokenQuery request, CancellationToken ct) =>
        await db.Set<User>().AsNoTracking().AnyAsync(u => u.Id == request.UserId, ct);
}
