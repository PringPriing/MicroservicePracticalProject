namespace ProductCatalog.Application.DTOs;

public record CategoryDto(Guid Id, string Name, Guid? ParentId);
