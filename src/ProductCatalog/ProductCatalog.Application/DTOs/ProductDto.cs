namespace ProductCatalog.Application.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    List<string> ImageUrls,
    Dictionary<string, string> Attributes,
    int InventoryCount,
    DateTime LastUpdated);
