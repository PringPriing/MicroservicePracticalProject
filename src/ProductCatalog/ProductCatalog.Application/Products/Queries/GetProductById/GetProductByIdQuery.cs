using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Application.Products.Queries.GetProductById;

public record GetProductByIdQuery(Guid ProductId) : IRequest<ProductDto?>;

public sealed class GetProductByIdQueryHandler(DbContext db)
    : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken ct) =>
        await db.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Id == request.ProductId)
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
            .FirstOrDefaultAsync(ct);
}
