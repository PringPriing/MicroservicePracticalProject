namespace UserManagement.Application.DTOs;

public record UserDto(Guid Id, string UserName, string FirstName, string LastName, string PhoneNumber, DateOnly DateOfBirth);
