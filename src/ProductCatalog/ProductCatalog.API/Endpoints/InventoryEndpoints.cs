using MediatR;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Inventory.Commands.UpdateInventory;
using Shared.Kernel.Exceptions;

namespace ProductCatalog.API.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/inventory/{productId}", async (string productId, UpdateInventoryRequest body, IMediator mediator, CancellationToken ct) =>
        {
            if (!Guid.TryParse(productId, out Guid id))
                throw new BadRequestException($"'{productId}' is not a valid product id.");

            InventoryDto result = await mediator.Send(new UpdateInventoryCommand(id, body.QuantityDelta, body.SetQuantity), ct);
            return Results.Ok(result);
        })
        .WithName("UpdateInventory");

        return app;
    }
}

internal record UpdateInventoryRequest(int? QuantityDelta, int? SetQuantity);
