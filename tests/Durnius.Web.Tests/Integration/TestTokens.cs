using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Durnius.Web.Tests.Integration;

/// <summary>Creates signed tokens with controllable claims for auth tests.</summary>
public static class TestTokens
{
    public static string Create(
        string username = "Alice",
        int userId = 1,
        string issuer = TestAppFactory.JwtIssuer,
        string audience = TestAppFactory.JwtAudience,
        string key = TestAppFactory.JwtKey,
        DateTime? expires = null,
        bool includeSubject = true,
        bool includeName = true)
    {
        var claims = new List<Claim>();
        if (includeSubject)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()));
        }

        if (includeName)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, username));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
