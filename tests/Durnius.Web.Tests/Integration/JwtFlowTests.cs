using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Durnius.Web.Data;
using Durnius.Web.Features.Account;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Durnius.Web.Tests.Integration;

public class JwtFlowTests
{
    /// <summary>Isolated from environment variables set by TestAppFactory.</summary>
    private sealed class StubConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values;

        public StubConfiguration(params (string Key, string? Value)[] values) =>
            _values = values.ToDictionary(value => value.Key, value => value.Value);

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var value) ? value : null;
            set => _values[key] = value!;
        }
        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => new ConfigurationReloadToken();
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }

    private static async Task<HttpResponseMessage> GetWithTokenAsync(
        HttpClient client, string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task ValidTokenIsAcceptedButInvalidIssuerAudienceKeyAndExpiryAreNot()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var registration = await client.PostAsJsonAsync(
            "/api/register", new { username = "Alice", password = "password" });
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        var valid = await GetWithTokenAsync(client, "/api/account/me", TestTokens.Create());
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);

        foreach (var badToken in new[]
        {
            "not-a-jwt",
            TestTokens.Create(key: "a-different-key-that-is-long-enough-for-hs256"),
            TestTokens.Create(issuer: "another-issuer"),
            TestTokens.Create(audience: "another-audience"),
            TestTokens.Create(expires: DateTime.UtcNow.AddMinutes(-5))
        })
        {
            var response = await GetWithTokenAsync(client, "/api/account/me", badToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task HubNegotiateAcceptsQueryTokenOnlyUnderHubPaths()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var token = TestTokens.Create();
        var negotiated = await client.PostAsync(
            $"/hubs/lobby/negotiate?access_token={Uri.EscapeDataString(token)}", content: null);

        Assert.Equal(HttpStatusCode.OK, negotiated.StatusCode);
        var negotiateBody = await negotiated.Content.ReadAsStringAsync();
        Assert.True(negotiateBody.Contains("connectionToken") || negotiateBody.Contains("connectionId"),
            negotiateBody);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/hubs/lobby/negotiate", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/hubs/lobby/negotiate?access_token=garbage", content: null)).StatusCode);

        // The query-string fallback is deliberately limited to /hubs.
        var apiWithQueryToken = await client.GetAsync(
            $"/api/account/me?access_token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.Unauthorized, apiWithQueryToken.StatusCode);
    }

    [Fact]
    public void TokenServiceSignsClaimsWithConfiguredIssuerAudienceAndExpiry()
    {
        using var factory = new TestAppFactory();
        using var scope = factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var token = tokens.GenerateToken(new User { Id = 42, Username = "Alice", PasswordHash = "x" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("42", jwt.Subject);
        Assert.Equal("Alice", jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Name).Value);
        Assert.Equal(TestAppFactory.JwtIssuer, jwt.Issuer);
        Assert.Equal(TestAppFactory.JwtAudience, Assert.Single(jwt.Audiences));
        Assert.InRange(jwt.ValidTo, DateTime.UtcNow.AddMinutes(55), DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public void MissingJwtConfigurationFailsFast()
    {
        Assert.Equal("Jwt:Key is not configured.", Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddJwtAuthentication(new StubConfiguration())).Message);

        var withKey = new StubConfiguration(("Jwt:Key", TestAppFactory.JwtKey));
        Assert.Equal("Jwt:Issuer is not configured.", Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddJwtAuthentication(withKey)).Message);

        var withIssuer = new StubConfiguration(
            ("Jwt:Key", TestAppFactory.JwtKey),
            ("Jwt:Issuer", TestAppFactory.JwtIssuer));
        Assert.Equal("Jwt:Audience is not configured.", Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddJwtAuthentication(withIssuer)).Message);
    }
}
