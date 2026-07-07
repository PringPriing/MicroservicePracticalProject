using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Application.Products.Queries.ListProducts;

public record ListProductsQuery(Guid? CategoryId, int Page, int Limit, string? Sort) : IRequest<PagedResult<ProductDto>>;

public sealed class ListProductsQueryHandler(DbContext db)
    : IRequestHandler<ListProductsQuery, PagedResult<ProductDto>>
{
    public const int MaxLimit = 100;

    public async Task<PagedResult<ProductDto>> Handle(ListProductsQuery request, CancellationToken ct)
    {
        int effectiveLimit = Math.Min(request.Limit, MaxLimit);

        IQueryable<Product> query = db.Set<Product>().AsNoTracking();

        if (request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);

        query = request.Sort switch
        {
            "price" => query.OrderBy(p => p.Price),
            _ => query.OrderBy(p => p.Name)
        };

        int totalCount = await query.CountAsync(ct);

        List<ProductDto> items = await query
            .Skip((request.Page - 1) * effectiveLimit)
            .Take(effectiveLimit)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.Currency,
                p.CategoryId,
                p.ImageUrls,
                p.Attributes,
                p.InventoryCount,
                p.LastUpdated))
            .ToListAsync(ct);

        return new PagedResult<ProductDto>(items, request.Page, effectiveLimit, totalCount);
    }
}

public sealed class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    private static readonly string[] SupportedSortValues = ["price"];

    public ListProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Limit).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Sort)
            .Must(sort => sort is null || SupportedSortValues.Contains(sort))
            .WithMessage($"Unsupported sort value. Supported values: {string.Join(", ", SupportedSortValues)}.");
    }
}
