using MediatR;
using UserManagement.Application.DTOs;

namespace UserManagement.Application.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid UserId) : IRequest<UserDto?>;
