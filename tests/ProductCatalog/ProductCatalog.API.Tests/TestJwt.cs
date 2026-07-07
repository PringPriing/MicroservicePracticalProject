using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ProductCatalog.API.Tests;

// ProductCatalog only validates JWTs, it never issues them (that's UserManagement's job), so there's
// no production ITokenService to reuse here — this mints tokens the same way JwtTokenService does,
// using the same Jwt:Key/Issuer/Audience ProductCatalog.API validates against.
internal static class TestJwt
{
    public static string MintToken(IConfiguration config, Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        Claim[] claims = [new(JwtRegisteredClaimNames.Sub, userId.ToString())];

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
