using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;
using Shared.Kernel.Exceptions;

namespace ProductCatalog.Application.Inventory.Commands.UpdateInventory;

public record UpdateInventoryCommand(Guid ProductId, int? QuantityDelta, int? SetQuantity) : IRequest<InventoryDto>;

public sealed class UpdateInventoryCommandHandler(DbContext db)
    : IRequestHandler<UpdateInventoryCommand, InventoryDto>
{
    public async Task<InventoryDto> Handle(UpdateInventoryCommand request, CancellationToken ct)
    {
        bool exists = await db.Set<Product>().AsNoTracking().AnyAsync(p => p.Id == request.ProductId, ct);
        if (!exists)
            throw new NotFoundException($"Product {request.ProductId} was not found.");

        DateTime now = DateTime.UtcNow;
        int rowsAffected;

        // A single atomic UPDATE guarded by a WHERE clause — never load-then-save the entity here,
        // or concurrent requests could both read the same count and both apply their delta on top of it.
        if (request.SetQuantity.HasValue)
        {
            rowsAffected = await db.Set<Product>()
                .Where(p => p.Id == request.ProductId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.InventoryCount, request.SetQuantity.Value)
                    .SetProperty(p => p.LastUpdated, now), ct);
        }
        else
        {
            int delta = request.QuantityDelta!.Value;

            rowsAffected = await db.Set<Product>()
                .Where(p => p.Id == request.ProductId && p.InventoryCount + delta >= 0)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.InventoryCount, p => p.InventoryCount + delta)
                    .SetProperty(p => p.LastUpdated, now), ct);

            if (rowsAffected == 0)
                throw new ConflictException(
                    $"Applying delta {delta} to product {request.ProductId} would make inventory negative.");
        }

        if (rowsAffected == 0)
            throw new NotFoundException($"Product {request.ProductId} was not found.");

        Product updated = await db.Set<Product>().AsNoTracking().FirstAsync(p => p.Id == request.ProductId, ct);
        return new InventoryDto(updated.Id, updated.InventoryCount, updated.LastUpdated);
    }
}

public sealed class UpdateInventoryCommandValidator : AbstractValidator<UpdateInventoryCommand>
{
    public UpdateInventoryCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.QuantityDelta.HasValue ^ x.SetQuantity.HasValue)
            .WithMessage("Exactly one of quantityDelta or setQuantity must be provided.");

        RuleFor(x => x.SetQuantity)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SetQuantity.HasValue)
            .WithMessage("setQuantity must not be negative.");
    }
}
