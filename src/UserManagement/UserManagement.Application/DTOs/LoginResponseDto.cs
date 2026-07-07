namespace UserManagement.Application.DTOs;

public record LoginResponseDto(string Token, Guid UserId, string UserName);
