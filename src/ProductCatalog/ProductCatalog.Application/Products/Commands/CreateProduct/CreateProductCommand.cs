using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;
using Shared.Kernel.Exceptions;

namespace ProductCatalog.Application.Products.Commands.CreateProduct;

public record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    List<string> ImageUrls,
    Dictionary<string, string> Attributes,
    int InventoryCount) : IRequest<ProductDto>;

public sealed class CreateProductCommandHandler(DbContext db)
    : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken ct)
    {
        bool categoryExists = await db.Set<Category>()
            .AnyAsync(c => c.Id == request.CategoryId, ct);

        if (!categoryExists)
            throw new NotFoundException($"Category {request.CategoryId} was not found.");

        Product product = Product.Create(
            request.Name,
            request.Description,
            request.Price,
            request.Currency,
            request.CategoryId,
            request.ImageUrls,
            request.Attributes,
            request.InventoryCount);

        db.Set<Product>().Add(product);
        await db.SaveChangesAsync(ct);

        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Currency,
            product.CategoryId,
            product.ImageUrls,
            product.Attributes,
            product.InventoryCount,
            product.LastUpdated);
    }
}

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).Length(3);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.InventoryCount).GreaterThanOrEqualTo(0);
    }
}
