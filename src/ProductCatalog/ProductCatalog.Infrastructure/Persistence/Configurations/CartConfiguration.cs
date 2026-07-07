using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.HasKey(c => c.UserId);

        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.CartUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
