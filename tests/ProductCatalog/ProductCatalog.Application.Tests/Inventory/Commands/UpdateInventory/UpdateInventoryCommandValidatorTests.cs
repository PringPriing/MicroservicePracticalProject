using FluentAssertions;
using FluentValidation.Results;
using ProductCatalog.Application.Inventory.Commands.UpdateInventory;
using Xunit;

namespace ProductCatalog.Application.Tests.Inventory.Commands.UpdateInventory;

public class UpdateInventoryCommandValidatorTests
{
    private readonly UpdateInventoryCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenNeitherDeltaNorSetQuantityProvided_ReturnsValidationError()
    {
        ValidationResult result = _validator.Validate(new UpdateInventoryCommand(Guid.NewGuid(), null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenBothDeltaAndSetQuantityProvided_ReturnsValidationError()
    {
        ValidationResult result = _validator.Validate(new UpdateInventoryCommand(Guid.NewGuid(), 1, 1));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenSetQuantityIsNegative_ReturnsValidationError()
    {
        ValidationResult result = _validator.Validate(new UpdateInventoryCommand(Guid.NewGuid(), null, -1));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenOnlyDeltaProvided_IsValid()
    {
        ValidationResult result = _validator.Validate(new UpdateInventoryCommand(Guid.NewGuid(), -3, null));

        result.IsValid.Should().BeTrue();
    }
}
