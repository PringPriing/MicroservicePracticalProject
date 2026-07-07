using MediatR;
using ProductCatalog.Application.Categories.Commands.CreateCategory;
using ProductCatalog.Application.DTOs;

namespace ProductCatalog.API.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories");

        group.MapPost("/", async (CreateCategoryRequest body, IMediator mediator, CancellationToken ct) =>
        {
            CategoryDto category = await mediator.Send(new CreateCategoryCommand(body.Name, body.ParentId), ct);
            return Results.Created($"/categories/{category.Id}", category);
        })
        .RequireAuthorization()
        .WithName("CreateCategory");

        return app;
    }
}

internal record CreateCategoryRequest(string Name, Guid? ParentId);
