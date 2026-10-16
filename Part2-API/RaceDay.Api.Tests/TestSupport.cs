using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests;

/// <summary>
/// Boots the real API in memory, swapping SQL Server for an isolated EF Core InMemory database.
/// Sessions, role checks, validation and controllers are all the real thing.
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "RaceDayTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated(); // seeds the two roles
        return host;
    }

    /// <summary>A brand-new client = a brand-new browser (its own session cookie).</summary>
    public HttpClient NewClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Direct database access for arranging test data the API deliberately does not allow (e.g. past events).</summary>
    public async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

public static class Helpers
{
    public const string Password = "Password123!";

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@test.co.za";

    public static object RegisterBody(string role, string? email = null, string password = Password) => new
    {
        firstName = "Test",
        lastName = role,
        email = email ?? UniqueEmail(),
        password,
        role
    };

    /// <summary>Registers a user and logs them in on a fresh client. Returns the client and the user's email.</summary>
    public static async Task<(HttpClient Client, string Email)> LoginAs(RaceDayFactory factory, string role)
    {
        var client = factory.NewClient();
        var email = UniqueEmail();
        var reg = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(role, email));
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (client, email);
    }

    public static async Task<HttpClient> Organiser(RaceDayFactory f) => (await LoginAs(f, "Organiser")).Client;
    public static async Task<HttpClient> Participant(RaceDayFactory f) => (await LoginAs(f, "Participant")).Client;

    public static object EventBody(string? name = null, DateTime? date = null, string type = "Run") => new
    {
        name = name ?? "Test Event " + Guid.NewGuid().ToString("N")[..6],
        description = "A test event",
        eventDate = (date ?? DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-ddTHH:mm:ss"),
        location = "Cape Town",
        distanceKm = 10.0,
        eventType = type
    };

    public static async Task<int> GetInt(HttpResponseMessage response, string property)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty(property).GetInt32();
    }

    public static async Task<int> CreateEvent(HttpClient organiser, string type = "Run")
    {
        var resp = await organiser.PostAsJsonAsync("/api/events", EventBody(type: type));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return await GetInt(resp, "eventId");
    }

    public static async Task<int> CreateCategory(HttpClient organiser, int eventId, string? name = null)
    {
        var resp = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = name ?? "Senior " + Guid.NewGuid().ToString("N")[..4], categoryType = "Age" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return await GetInt(resp, "categoryId");
    }

    public static async Task<int> Enrol(HttpClient participant, int eventId, int categoryId)
    {
        var resp = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return await GetInt(resp, "enrolmentId");
    }

    public static Task MakeEventPast(RaceDayFactory factory, int eventId) =>
        factory.WithDb(async db =>
        {
            var ev = await db.Events.SingleAsync(e => e.EventId == eventId);
            ev.EventDate = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        });
}
