using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Messaging;
using UserManagement.API.Endpoints;
using UserManagement.Application.Behaviors;
using UserManagement.Application.Exceptions;
using UserManagement.Application.Users.Commands.RegisterUser;
using UserManagement.Infrastructure.Messaging;
using UserManagement.Infrastructure.Persistence;
using UserManagement.Infrastructure.Services;
using UserManagement.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<UserManagementDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<UserManagementDbContext>());

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(RegisterUserCommand).Assembly);

builder.Services.AddScoped<ITokenService, JwtTokenService>();

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Without this, the handler's default inbound claim mapping silently renames "sub" (and other
        // short JWT claim names) to legacy long-form URIs, so FindFirstValue(JwtRegisteredClaimNames.Sub)
        // in the endpoints below returns null for every authenticated request.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.UseExceptionHandler(errApp =>
    errApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        switch (feature?.Error)
        {
            case ValidationException validationEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";
                var errors = validationEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                await context.Response.WriteAsJsonAsync(new { title = "Validation failed", status = 400, errors });
                break;

            case UnauthorizedAccessException:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { title = "Unauthorized", status = 401 });
                break;

            case NotFoundException notFoundEx:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { title = "Not Found", status = 404, message = notFoundEx.Message });
                break;

            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { title = "An unexpected error occurred", status = 500 });
                break;
        }
    }));

app.MapUserEndpoints();

// Off by default (local dotnet run and WebApplicationFactory tests both rely on their existing
// setup — manual `dotnet ef database update` locally, EnsureCreated() in tests). Only the
// Kubernetes ConfigMaps set this to true, since Migrate() is idempotent and safe on every pod start.
if (builder.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using IServiceScope scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<UserManagementDbContext>().Database.Migrate();
}

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap this host in integration tests.
public partial class Program;
