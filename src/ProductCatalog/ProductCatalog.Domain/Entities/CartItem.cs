namespace ProductCatalog.Domain.Entities;

public sealed class CartItem
{
    public Guid CartUserId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }

    private CartItem() { }

    internal static CartItem Create(Guid cartUserId, Guid productId, int quantity) => new()
    {
        CartUserId = cartUserId,
        ProductId = productId,
        Quantity = quantity
    };

    internal void SetQuantity(int quantity) => Quantity = quantity;
}
