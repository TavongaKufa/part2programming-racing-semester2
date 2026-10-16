using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Api.Tests;

/// <summary>Event management: Organisers manage their own events, both roles can view.</summary>
public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public EventTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        var organiser = await Helpers.Organiser(_factory);

        var response = await organiser.PostAsJsonAsync("/api/events", Helpers.EventBody("Soweto 10km"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Soweto 10km", json.GetProperty("name").GetString());
        Assert.Equal("Run", json.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_ReturnsForbidden()
    {
        var participant = await Helpers.Participant(_factory);

        var response = await participant.PostAsJsonAsync("/api/events", Helpers.EventBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CannotCreateEvent_ReturnsUnauthorized()
    {
        var response = await _factory.NewClient().PostAsJsonAsync("/api/events", Helpers.EventBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_MissingName_ReturnsBadRequest()
    {
        var organiser = await Helpers.Organiser(_factory);

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            description = "No name",
            eventDate = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-ddTHH:mm:ss"),
            location = "Durban",
            distanceKm = 5,
            eventType = "Walk"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_InvalidEventType_ReturnsBadRequest()
    {
        var organiser = await Helpers.Organiser(_factory);

        var response = await organiser.PostAsJsonAsync("/api/events", Helpers.EventBody(type: "Swim"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_DateInThePast_ReturnsBadRequest()
    {
        var organiser = await Helpers.Organiser(_factory);

        var response = await organiser.PostAsJsonAsync("/api/events",
            Helpers.EventBody(date: DateTime.UtcNow.AddDays(-5)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_ZeroDistance_ReturnsBadRequest()
    {
        var organiser = await Helpers.Organiser(_factory);

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Zero",
            description = "Invalid distance",
            eventDate = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-ddTHH:mm:ss"),
            location = "Durban",
            distanceKm = 0,
            eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BothRoles_CanViewEvents()
    {
        var organiser = await Helpers.Organiser(_factory);
        var participant = await Helpers.Participant(_factory);
        var eventId = await Helpers.CreateEvent(organiser);

        var asOrganiser = await organiser.GetAsync($"/api/events/{eventId}");
        var asParticipant = await participant.GetAsync($"/api/events/{eventId}");
        var list = await participant.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, asOrganiser.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asParticipant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task GetEvents_FilterByType_ReturnsOnlyThatType()
    {
        var organiser = await Helpers.Organiser(_factory);
        await Helpers.CreateEvent(organiser, "Run");
        var cycleId = await Helpers.CreateEvent(organiser, "Cycle");

        var json = await (await organiser.GetAsync("/api/events?type=Cycle")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.GetArrayLength() >= 1);
        Assert.All(json.EnumerateArray(), e => Assert.Equal("Cycle", e.GetProperty("eventType").GetString()));
        Assert.Contains(json.EnumerateArray(), e => e.GetProperty("eventId").GetInt32() == cycleId);
    }

    [Fact]
    public async Task GetEvent_DoesNotExist_ReturnsNotFound()
    {
        var organiser = await Helpers.Organiser(_factory);

        var response = await organiser.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_MyEvents_OnlyListsOwnEvents()
    {
        var first = await Helpers.Organiser(_factory);
        var second = await Helpers.Organiser(_factory);
        await Helpers.CreateEvent(first);
        await Helpers.CreateEvent(second);
        await Helpers.CreateEvent(second);

        var json = await (await second.GetAsync("/api/events/mine")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, json.GetArrayLength());
    }

    [Fact]
    public async Task Participant_MyEvents_ReturnsForbidden()
    {
        var participant = await Helpers.Participant(_factory);

        var response = await participant.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var organiser = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{eventId}", Helpers.EventBody("Renamed Event", type: "Walk"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed Event", json.GetProperty("name").GetString());
        Assert.Equal("Walk", json.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await Helpers.Organiser(_factory);
        var other = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(owner);

        var response = await other.PutAsJsonAsync($"/api/events/{eventId}", Helpers.EventBody("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateEvent_ReturnsForbidden()
    {
        var organiser = await Helpers.Organiser(_factory);
        var participant = await Helpers.Participant(_factory);
        var eventId = await Helpers.CreateEvent(organiser);

        var response = await participant.PutAsJsonAsync($"/api/events/{eventId}", Helpers.EventBody("Nope"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_DoesNotExist_ReturnsNotFound()
    {
        var organiser = await Helpers.Organiser(_factory);

        var response = await organiser.PutAsJsonAsync("/api/events/999999", Helpers.EventBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var organiser = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(organiser);
        await Helpers.CreateCategory(organiser, eventId);

        var delete = await organiser.DeleteAsync($"/api/events/{eventId}");
        var get = await organiser.GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await Helpers.Organiser(_factory);
        var other = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(owner);

        var response = await other.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
