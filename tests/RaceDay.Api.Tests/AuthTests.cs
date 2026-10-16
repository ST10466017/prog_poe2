using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace RaceDay.Api.Tests;

/// <summary>User registration, login/logout, session handling and profile.</summary>
public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public AuthTests(RaceDayFactory factory) => _factory = factory;

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task Register_WithValidRole_Returns201_AndNeverReturnsPassword(string role)
    {
        var client = _factory.CreateClient();
        var response = await TestApi.Register(client, role);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(role, body);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = TestApi.UniqueEmail("dup");
        Assert.Equal(HttpStatusCode.Created, (await TestApi.Register(client, "Participant", email)).StatusCode);

        var second = await TestApi.Register(client, "Organiser", email);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("")]
    [InlineData("SuperUser")]
    public async Task Register_WithInvalidRole_Returns400(string role)
    {
        var response = await TestApi.Register(_factory.CreateClient(), role);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400()
    {
        var response = await TestApi.Register(_factory.CreateClient(), "Participant", password: "short");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns400()
    {
        var response = await TestApi.Register(_factory.CreateClient(), "Participant", email: "not-an-email");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var client = _factory.CreateClient();
        var email = TestApi.UniqueEmail("hash");
        await TestApi.Register(client, "Participant", email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = await db.Users.Where(u => u.Email == email).Select(u => u.PasswordHash).SingleAsync();

        Assert.NotEqual(TestApi.Password, stored);
        Assert.DoesNotContain(TestApi.Password, stored);
        Assert.True(PasswordHasher.Verify(TestApi.Password, stored));
        Assert.False(PasswordHasher.Verify("wrong-password", stored));
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200_AndStartsSession()
    {
        var (client, _, _) = await TestApi.LoginAs(_factory, "Participant");

        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = TestApi.UniqueEmail("wrongpw");
        await TestApi.Register(client, "Participant", email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong-Password-1" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        // and no session was created
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = TestApi.UniqueEmail("ghost"), password = TestApi.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutLogin_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var (client, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Profile_CanBeViewedAndUpdated_ByBothRoles()
    {
        foreach (var role in new[] { "Organiser", "Participant" })
        {
            var (client, _, _) = await TestApi.LoginAs(_factory, role);

            var update = await client.PutAsJsonAsync("/api/users/me", new { fullName = "New Name", phone = "0839998888" });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);

            var me = await TestApi.Json(await client.GetAsync("/api/users/me"));
            Assert.Equal("New Name", me.GetProperty("fullName").GetString());
            Assert.Equal(role, me.GetProperty("role").GetString());
        }
    }

    [Fact]
    public async Task Profile_Update_WithInvalidData_Returns400()
    {
        var (client, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var response = await client.PutAsJsonAsync("/api/users/me", new { fullName = "", phone = "1" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TwoClients_KeepSeparateSessions()
    {
        var (organiser, _, orgEmail) = await TestApi.LoginAs(_factory, "Organiser");
        var (participant, _, partEmail) = await TestApi.LoginAs(_factory, "Participant");

        Assert.Equal("Organiser", (await TestApi.Json(await organiser.GetAsync("/api/users/me"))).GetProperty("role").GetString());
        Assert.Equal("Participant", (await TestApi.Json(await participant.GetAsync("/api/users/me"))).GetProperty("role").GetString());
        Assert.Equal(orgEmail, (await TestApi.Json(await organiser.GetAsync("/api/users/me"))).GetProperty("email").GetString());
        Assert.Equal(partEmail, (await TestApi.Json(await participant.GetAsync("/api/users/me"))).GetProperty("email").GetString());
    }
}
