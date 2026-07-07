using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Application.Carts.Commands.CreateCart;

public record CreateCartCommand(Guid UserId) : IRequest;

public sealed class CreateCartCommandHandler(DbContext db) : IRequestHandler<CreateCartCommand>
{
    public async Task Handle(CreateCartCommand request, CancellationToken ct)
    {
        bool cartExists = await db.Set<Cart>().AnyAsync(c => c.UserId == request.UserId, ct);
        if (cartExists)
            return;

        db.Set<Cart>().Add(Cart.Create(request.UserId));
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CreateCartCommandValidator : AbstractValidator<CreateCartCommand>
{
    public CreateCartCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
