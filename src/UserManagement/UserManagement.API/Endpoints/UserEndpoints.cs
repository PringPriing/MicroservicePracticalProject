using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using UserManagement.Application.DTOs;
using UserManagement.Application.Users.Commands.ChangePassword;
using UserManagement.Application.Users.Commands.LoginUser;
using UserManagement.Application.Users.Commands.RegisterUser;
using UserManagement.Application.Users.Commands.UpdateUserProfile;
using UserManagement.Application.Users.Queries.ValidateToken;

namespace UserManagement.API.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (RegisterUserCommand command, IMediator mediator, CancellationToken ct) =>
        {
            Guid id = await mediator.Send(command, ct);
            return Results.Created($"/auth/{id}", new { id });
        })
        .WithName("RegisterUser");

        group.MapPost("/login", async (LoginUserCommand command, IMediator mediator, CancellationToken ct) =>
        {
            LoginResponseDto result = await mediator.Send(command, ct);
            return Results.Ok(result);
        })
        .WithName("LoginUser");

        group.MapPut("/profile", async (UpdateProfileRequest body, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            Guid userId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            await mediator.Send(new UpdateUserProfileCommand(userId, body.FirstName, body.LastName, body.PhoneNumber, body.DateOfBirth), ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("UpdateUserProfile");

        group.MapPatch("/password", async (ChangePasswordRequest body, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            Guid userId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            await mediator.Send(new ChangePasswordCommand(userId, body.CurrentPassword, body.NewPassword), ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("ChangePassword");

        group.MapGet("/validate-token", async (ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            Guid userId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            bool exists = await mediator.Send(new ValidateTokenQuery(userId), ct);
            return exists ? Results.Ok() : Results.Unauthorized();
        })
        .RequireAuthorization()
        .WithName("ValidateToken");

        return app;
    }
}

internal record UpdateProfileRequest(string FirstName, string LastName, string PhoneNumber, DateOnly DateOfBirth);
internal record ChangePasswordRequest(string CurrentPassword, string NewPassword);
