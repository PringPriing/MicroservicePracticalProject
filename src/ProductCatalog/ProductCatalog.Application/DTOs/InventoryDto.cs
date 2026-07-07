namespace ProductCatalog.Application.DTOs;

public record InventoryDto(Guid ProductId, int InventoryCount, DateTime LastUpdated);
