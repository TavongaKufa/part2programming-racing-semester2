using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RaceDay.Api.Tests;

/// <summary>User registration, login, logout and "must be logged in" checks.</summary>
public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public AuthTests(RaceDayFactory factory) => _factory = factory;

    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    public async Task Register_ValidUser_ReturnsCreatedWithRole(string role)
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", Helpers.RegisterBody(role));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(role, json.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_PasswordIsHashedInDatabase()
    {
        var client = _factory.NewClient();
        var email = Helpers.UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", Helpers.RegisterBody("Participant", email));

        await _factory.WithDb(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Assert.NotEqual(Helpers.Password, user.PasswordHash);
            Assert.DoesNotContain(Helpers.Password, user.PasswordHash);
        });
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = _factory.NewClient();
        var email = Helpers.UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", Helpers.RegisterBody("Participant", email));

        var second = await client.PostAsJsonAsync("/api/auth/register", Helpers.RegisterBody("Organiser", email));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownRole_ReturnsBadRequest()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", Helpers.RegisterBody("Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            Helpers.RegisterBody("Participant", password: "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            Helpers.RegisterBody("Participant", email: "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkAndStartsSession()
    {
        var (client, _) = await Helpers.LoginAs(_factory, "Participant");

        var profile = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.NewClient();
        var email = Helpers.UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", Helpers.RegisterBody("Participant", email));

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = Helpers.UniqueEmail(), password = Helpers.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession_ProtectedEndpointsReturnUnauthorized()
    {
        var (client, _) = await Helpers.LoginAs(_factory, "Organiser");

        var logout = await client.PostAsync("/api/auth/logout", null);
        var afterLogout = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Theory]
    [InlineData("/api/profile")]
    [InlineData("/api/events")]
    [InlineData("/api/events/1")]
    [InlineData("/api/events/1/categories")]
    [InlineData("/api/events/mine")]
    [InlineData("/api/enrolments/mine")]
    [InlineData("/api/results/mine")]
    [InlineData("/api/events/1/results")]
    public async Task ProtectedEndpoints_WithoutLogin_ReturnUnauthorized(string url)
    {
        var client = _factory.NewClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_Get_ReturnsOwnDetails()
    {
        var (client, email) = await Helpers.LoginAs(_factory, "Participant");

        var json = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(email, json.GetProperty("email").GetString());
        Assert.Equal("Participant", json.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Profile_Update_ChangesAreSaved()
    {
        var (client, _) = await Helpers.LoginAs(_factory, "Participant");

        var update = await client.PutAsJsonAsync("/api/profile",
            new { firstName = "Zanele", lastName = "Khumalo", phoneNumber = "0821112222" });
        var json = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Zanele", json.GetProperty("firstName").GetString());
        Assert.Equal("0821112222", json.GetProperty("phoneNumber").GetString());
    }

    [Fact]
    public async Task Profile_Update_MissingFirstName_ReturnsBadRequest()
    {
        var (client, _) = await Helpers.LoginAs(_factory, "Participant");

        var response = await client.PutAsJsonAsync("/api/profile", new { firstName = "", lastName = "Khumalo" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
