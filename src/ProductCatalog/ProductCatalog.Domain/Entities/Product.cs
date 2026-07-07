namespace ProductCatalog.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public List<string> ImageUrls { get; private set; } = [];
    public Dictionary<string, string> Attributes { get; private set; } = [];
    public int InventoryCount { get; private set; }
    public DateTime LastUpdated { get; private set; }

    private Product() { }

    public void UpdateDetails(
        string name,
        string description,
        decimal price,
        string currency,
        Guid categoryId,
        List<string> imageUrls,
        Dictionary<string, string> attributes)
    {
        Name = name;
        Description = description;
        Price = price;
        Currency = currency;
        CategoryId = categoryId;
        ImageUrls = imageUrls;
        Attributes = attributes;
        LastUpdated = DateTime.UtcNow;
    }

    public static Product Create(
        string name,
        string description,
        decimal price,
        string currency,
        Guid categoryId,
        List<string> imageUrls,
        Dictionary<string, string> attributes,
        int inventoryCount)
    {
        return new Product
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Description = description,
            Price = price,
            Currency = currency,
            CategoryId = categoryId,
            ImageUrls = imageUrls,
            Attributes = attributes,
            InventoryCount = inventoryCount,
            LastUpdated = DateTime.UtcNow
        };
    }
}
