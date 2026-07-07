using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProductCatalog.API.Endpoints;
using ProductCatalog.Application.Carts.Commands.AddCartItem;
using ProductCatalog.Infrastructure.Messaging;
using ProductCatalog.Infrastructure.Persistence;
using Shared.Kernel;
using Shared.Kernel.Behaviors;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<ProductCatalogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ProductCatalogDbContext>());

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(AddCartItemCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(AddCartItemCommand).Assembly);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddHostedService<UserRegisteredConsumer>();

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
        context.Response.ContentType = "application/json";

        ErrorResponse response;
        switch (feature?.Error)
        {
            case ValidationException validationEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response = new ErrorResponse(new ErrorDetail(
                    "VALIDATION_ERROR",
                    string.Join(" ", validationEx.Errors.Select(e => e.ErrorMessage))));
                break;

            case BadRequestException badRequestEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response = new ErrorResponse(new ErrorDetail("BAD_REQUEST", badRequestEx.Message));
                break;

            case NotFoundException notFoundEx:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                response = new ErrorResponse(new ErrorDetail("NOT_FOUND", notFoundEx.Message));
                break;

            case ConflictException conflictEx:
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                response = new ErrorResponse(new ErrorDetail("CONFLICT", conflictEx.Message));
                break;

            case JsonException:
            case BadHttpRequestException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response = new ErrorResponse(new ErrorDetail("MALFORMED_REQUEST", "The request body could not be parsed."));
                break;

            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                response = new ErrorResponse(new ErrorDetail("INTERNAL_ERROR", "An unexpected error occurred."));
                break;
        }

        await context.Response.WriteAsJsonAsync(response);
    }));

app.MapProductEndpoints();
app.MapCartEndpoints();
app.MapInventoryEndpoints();
app.MapCategoryEndpoints();

// Off by default (local dotnet run and WebApplicationFactory tests both rely on their existing
// setup — manual `dotnet ef database update` locally, EnsureCreated() in tests). Only the
// Kubernetes ConfigMaps set this to true, since Migrate() is idempotent and safe on every pod start.
if (builder.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using IServiceScope scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>().Database.Migrate();
}

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap this host in integration tests.
public partial class Program;
