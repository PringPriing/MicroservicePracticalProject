using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using ProductCatalog.Application.Carts.Commands.AddCartItem;
using ProductCatalog.Application.DTOs;

namespace ProductCatalog.API.Endpoints;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cart");

        group.MapPost("/items", async (AddCartItemRequest body, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            Guid userId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            CartDto cart = await mediator.Send(new AddCartItemCommand(userId, body.ProductId, body.Quantity), ct);
            return Results.Ok(cart);
        })
        .RequireAuthorization()
        .WithName("AddCartItem");

        return app;
    }
}

internal record AddCartItemRequest(Guid ProductId, int Quantity);
