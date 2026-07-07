using MediatR;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Products.Commands.CreateProduct;
using ProductCatalog.Application.Products.Queries.GetProductById;
using ProductCatalog.Application.Products.Queries.ListProducts;
using Shared.Kernel.Exceptions;

namespace ProductCatalog.API.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products");

        group.MapGet("/{productId}", async (string productId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (!Guid.TryParse(productId, out Guid id))
                throw new BadRequestException($"'{productId}' is not a valid product id.");

            ProductDto product = await mediator.Send(new GetProductByIdQuery(id), ct)
                ?? throw new NotFoundException($"Product {id} was not found.");

            string etag = $"\"{product.LastUpdated.Ticks}\"";
            http.Response.Headers.ETag = etag;

            if (http.Request.Headers.IfNoneMatch == etag)
                return Results.StatusCode(StatusCodes.Status304NotModified);

            if (DateTimeOffset.TryParse(http.Request.Headers.IfModifiedSince, out DateTimeOffset since) &&
                product.LastUpdated <= since.UtcDateTime)
                return Results.StatusCode(StatusCodes.Status304NotModified);

            return Results.Ok(product);
        })
        .WithName("GetProductById");

        group.MapGet("/", async (string? category, string? page, string? limit, string? sort, IMediator mediator, CancellationToken ct) =>
        {
            Guid? categoryId = null;
            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!Guid.TryParse(category, out Guid parsedCategory))
                    throw new BadRequestException($"'{category}' is not a valid category id.");
                categoryId = parsedCategory;
            }

            int pageNumber = 1;
            if (!string.IsNullOrWhiteSpace(page) && !int.TryParse(page, out pageNumber))
                throw new BadRequestException($"'{page}' is not a valid page number.");

            int limitValue = 20;
            if (!string.IsNullOrWhiteSpace(limit) && !int.TryParse(limit, out limitValue))
                throw new BadRequestException($"'{limit}' is not a valid limit.");

            PagedResult<ProductDto> result =
                await mediator.Send(new ListProductsQuery(categoryId, pageNumber, limitValue, sort), ct);
            return Results.Ok(result);
        })
        .WithName("ListProducts");

        group.MapPost("/", async (CreateProductRequest body, IMediator mediator, CancellationToken ct) =>
        {
            ProductDto product = await mediator.Send(
                new CreateProductCommand(
                    body.Name,
                    body.Description,
                    body.Price,
                    body.Currency,
                    body.CategoryId,
                    body.ImageUrls ?? [],
                    body.Attributes ?? [],
                    body.InventoryCount),
                ct);

            return Results.CreatedAtRoute("GetProductById", new { productId = product.Id }, product);
        })
        .RequireAuthorization()
        .WithName("CreateProduct");

        return app;
    }
}

internal record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    List<string>? ImageUrls,
    Dictionary<string, string>? Attributes,
    int InventoryCount);
