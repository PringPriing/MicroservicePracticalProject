namespace ProductCatalog.Application.DTOs;

public record CartDto(Guid UserId, List<CartItemDto> Items, DateTime UpdatedAt, UserProfileDto? Owner);

public record CartItemDto(Guid ProductId, int Quantity);
