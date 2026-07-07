using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Domain.Entities;
using Shared.Kernel.Exceptions;

namespace ProductCatalog.Application.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(string Name, Guid? ParentId) : IRequest<CategoryDto>;

public sealed class CreateCategoryCommandHandler(DbContext db)
    : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        if (request.ParentId is Guid parentId)
        {
            bool parentExists = await db.Set<Category>().AnyAsync(c => c.Id == parentId, ct);
            if (!parentExists)
                throw new NotFoundException($"Category {parentId} was not found.");
        }

        Category category = Category.Create(request.Name, request.ParentId);

        db.Set<Category>().Add(category);
        await db.SaveChangesAsync(ct);

        return new CategoryDto(category.Id, category.Name, category.ParentId);
    }
}

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
