using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Durnius.Web.Features.Account;
using Xunit;

namespace Durnius.Web.Tests.Integration;

public class AccountFlowTests
{
    private static async Task<(HttpResponseMessage Response, AuthResponse? Body)> RegisterAsync(
        HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/register", new { username, password });
        var body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AuthResponse>()
            : null;
        return (response, body);
    }

    private static async Task<string[]> ErrorsAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToArray();
    }

    [Fact]
    public async Task RegisterLoginMeChangePasswordAndMatchHistoryFlow()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var (registration, auth) = await RegisterAsync(client, "Alice", "correct horse battery staple");

        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        Assert.NotNull(auth);
        Assert.Equal("Bearer", auth.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth.Token);
        Assert.Equal("1", jwt.Subject);
        Assert.Equal("Alice", jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Name).Value);
        Assert.Equal(TestAppFactory.JwtIssuer, jwt.Issuer);
        Assert.Equal(TestAppFactory.JwtAudience, Assert.Single(jwt.Audiences));
        Assert.InRange(jwt.ValidTo, DateTime.UtcNow.AddMinutes(50), DateTime.UtcNow.AddMinutes(61));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);

        client.WithToken(auth.Token);
        var me = await client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var account = await me.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);
        Assert.Equal("Alice", account.Username);
        Assert.Equal(0, account.Gold);

        var history = await client.GetAsync("/api/account/matches");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Empty(await history.Content.ReadFromJsonAsync<List<MatchHistoryEntryDto>>() ?? []);

        var wrongChange = await client.PutAsJsonAsync(
            "/api/account/password",
            new { currentPassword = "not-my-password", newPassword = "new-password" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongChange.StatusCode);
        Assert.Contains("The current password is incorrect.", await ErrorsAsync(wrongChange));

        var change = await client.PutAsJsonAsync(
            "/api/account/password",
            new { currentPassword = "correct horse battery staple", newPassword = "new-password" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var oldLogin = await client.PostAsJsonAsync(
            "/api/login", new { username = "Alice", password = "correct horse battery staple" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync(
            "/api/login", new { username = "Alice", password = "new-password" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        Assert.NotNull(await newLogin.Content.ReadFromJsonAsync<AuthResponse>());
    }

    [Fact]
    public async Task RegisterAndLoginRejectInvalidAndUnknownCredentials()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var invalid = await client.PostAsJsonAsync(
            "/api/register", new { username = "  ", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("Username is required.", await ErrorsAsync(invalid));
        Assert.Contains("Password is required.", await ErrorsAsync(invalid));

        var tooLong = await client.PostAsJsonAsync(
            "/api/register", new { username = new string('a', 33), password = new string('p', 73) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var longErrors = await ErrorsAsync(tooLong);
        Assert.Contains("Username must be at most 32 characters.", longErrors);
        Assert.Contains("Password must be at most 72 characters.", longErrors);

        var (first, _) = await RegisterAsync(client, "Alice", "password");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicate = await RegisterAsync(client, "Alice", "other-password");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.Response.StatusCode);
        Assert.Contains("Username already taken.", await ErrorsAsync(duplicate.Response));

        var unknown = await client.PostAsJsonAsync(
            "/api/login", new { username = "Ghost", password = "password" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);

        var wrongPassword = await client.PostAsJsonAsync(
            "/api/login", new { username = "Alice", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Contains("Invalid username or password.", await ErrorsAsync(wrongPassword));

        var invalidLogin = await client.PostAsJsonAsync(
            "/api/login", new { username = "", password = "password" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidLogin.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedEndpointsRequireValidClaimsAndAnExistingAccount()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var (registration, _) = await RegisterAsync(client, "Alice", "password");
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/matches")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(
            "/api/account/password",
            new { currentPassword = "old", newPassword = "new" })).StatusCode);

        // The password and history endpoints authorize by the "sub" claim.
        var noSubject = TestTokens.Create(includeSubject: false);
        client.WithToken(noSubject);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/matches")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(
            "/api/account/password",
            new { currentPassword = "old", newPassword = "new" })).StatusCode);

        // "me" uses the name claim instead and works without a subject.
        var me = await client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("Alice", (await me.Content.ReadFromJsonAsync<AccountDto>())!.Username);

        client.WithToken(TestTokens.Create(username: "Ghost", userId: 999));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Empty(await client.GetFromJsonAsync<List<MatchHistoryEntryDto>>("/api/account/matches") ?? []);

        var unknownUserChange = await client.PutAsJsonAsync(
            "/api/account/password",
            new { currentPassword = "old", newPassword = "new" });
        Assert.Equal(HttpStatusCode.NotFound, unknownUserChange.StatusCode);

        client.WithToken(TestTokens.Create(includeName: false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
    }
}
