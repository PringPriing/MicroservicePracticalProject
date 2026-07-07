namespace ProductCatalog.Application.DTOs;

public record CartDto(Guid UserId, List<CartItemDto> Items, DateTime UpdatedAt);

public record CartItemDto(Guid ProductId, int Quantity);
