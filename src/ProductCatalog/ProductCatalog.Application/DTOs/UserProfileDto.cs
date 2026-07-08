namespace ProductCatalog.Application.DTOs;

public record UserProfileDto(Guid Id, string UserName, string FirstName, string LastName, string PhoneNumber, DateOnly DateOfBirth);
