namespace ProductCatalog.Domain.Entities;

public sealed class Cart
{
    public Guid UserId { get; private set; }
    public List<CartItem> Items { get; private set; } = [];
    public DateTime UpdatedAt { get; private set; }

    private Cart() { }

    public static Cart Create(Guid userId) => new()
    {
        UserId = userId,
        UpdatedAt = DateTime.UtcNow
    };

    public void AddOrUpdateItem(Guid productId, int additionalQuantity)
    {
        var existing = Items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is null)
            Items.Add(CartItem.Create(UserId, productId, additionalQuantity));
        else
            existing.SetQuantity(existing.Quantity + additionalQuantity);

        UpdatedAt = DateTime.UtcNow;
    }
}
