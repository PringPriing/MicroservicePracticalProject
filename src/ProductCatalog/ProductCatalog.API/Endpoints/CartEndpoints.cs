using MediatR;
using ProductCatalog.Application.Carts.Commands.AddCartItem;
using ProductCatalog.Application.DTOs;
using Shared.Kernel.Exceptions;

namespace ProductCatalog.API.Endpoints;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cart");

        group.MapPost("/{userId}/items", async (string userId, AddCartItemRequest body, IMediator mediator, CancellationToken ct) =>
        {
            if (!Guid.TryParse(userId, out Guid id))
                throw new BadRequestException($"'{userId}' is not a valid user id.");

            CartDto cart = await mediator.Send(new AddCartItemCommand(id, body.ProductId, body.Quantity), ct);
            return Results.Ok(cart);
        })
        .WithName("AddCartItem");

        return app;
    }
}

internal record AddCartItemRequest(Guid ProductId, int Quantity);
