using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Services;
using ProductCatalog.Domain.Entities;
using Shared.Kernel.Exceptions;

namespace ProductCatalog.Application.Carts.Commands.AddCartItem;

public record AddCartItemCommand(Guid UserId, Guid ProductId, int Quantity, string? BearerToken) : IRequest<CartDto>;

public sealed class AddCartItemCommandHandler(DbContext db, IUserManagementClient userManagementClient)
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

        UserProfileDto? owner = request.BearerToken is not null
            ? await userManagementClient.GetUserByIdAsync(request.UserId, request.BearerToken, ct)
            : null;

        return new CartDto(
            cart.UserId,
            cart.Items.Select(i => new CartItemDto(i.ProductId, i.Quantity)).ToList(),
            cart.UpdatedAt,
            owner);
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
