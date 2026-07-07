using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;
using Shared.Kernel.Exceptions;

namespace ProductCatalog.Application.Carts.Commands.AddCartItem;

public record AddCartItemCommand(Guid UserId, Guid ProductId, int Quantity) : IRequest<CartDto>;

public sealed class AddCartItemCommandHandler(DbContext db)
    : IRequestHandler<AddCartItemCommand, CartDto>
{
    public async Task<CartDto> Handle(AddCartItemCommand request, CancellationToken ct)
    {
        Product product = await db.Set<Product>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct)
            ?? throw new NotFoundException($"Product {request.ProductId} was not found.");

        Cart cart = await db.Set<Cart>()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == request.UserId, ct)
            ?? throw new NotFoundException($"User {request.UserId} was not found.");

        int existingQuantity = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId)?.Quantity ?? 0;
        int newTotalQuantity = existingQuantity + request.Quantity;

        if (newTotalQuantity > product.InventoryCount)
            throw new ConflictException(
                $"Only {product.InventoryCount} unit(s) of product {request.ProductId} are available.");

        cart.AddOrUpdateItem(request.ProductId, request.Quantity);
        await db.SaveChangesAsync(ct);

        return new CartDto(
            cart.UserId,
            cart.Items.Select(i => new CartItemDto(i.ProductId, i.Quantity)).ToList(),
            cart.UpdatedAt);
    }
}

public sealed class AddCartItemCommandValidator : AbstractValidator<AddCartItemCommand>
{
    public AddCartItemCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
