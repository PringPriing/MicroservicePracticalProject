namespace ProductCatalog.Domain.Entities;

public sealed class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }

    private Category() { }

    public static Category Create(string name, Guid? parentId = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        ParentId = parentId
    };
}
