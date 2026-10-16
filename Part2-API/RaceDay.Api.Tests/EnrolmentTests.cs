using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Api.Tests;

/// <summary>Event enrolments: Participants enrol, and the link Participant + Event + Category is recorded.</summary>
public class EnrolmentTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public EnrolmentTests(RaceDayFactory factory) => _factory = factory;

    private async Task<(HttpClient Organiser, int EventId, int CategoryId)> NewEventWithCategory()
    {
        var organiser = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(organiser);
        var categoryId = await Helpers.CreateCategory(organiser, eventId);
        return (organiser, eventId, categoryId);
    }

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecordedImmediately()
    {
        var (_, eventId, categoryId) = await NewEventWithCategory();
        var participant = await Helpers.Participant(_factory);

        var enrol = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        var mine = await (await participant.GetAsync("/api/enrolments/mine")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, enrol.StatusCode);
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(eventId, mine[0].GetProperty("eventId").GetInt32());
        Assert.Equal(categoryId, mine[0].GetProperty("categoryId").GetInt32());
        Assert.Equal("Confirmed", mine[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Organiser_CannotEnrol_ReturnsForbidden()
    {
        var (organiser, eventId, categoryId) = await NewEventWithCategory();

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CannotEnrol_ReturnsUnauthorized()
    {
        var (_, eventId, categoryId) = await NewEventWithCategory();

        var response = await _factory.NewClient()
            .PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_Twice_ReturnsConflict()
    {
        var (_, eventId, categoryId) = await NewEventWithCategory();
        var participant = await Helpers.Participant(_factory);
        await Helpers.Enrol(participant, eventId, categoryId);

        var second = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_CategoryFromDifferentEvent_ReturnsBadRequest()
    {
        var (_, eventId, _) = await NewEventWithCategory();
        var (_, _, otherCategoryId) = await NewEventWithCategory();
        var participant = await Helpers.Participant(_factory);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments",
            new { categoryId = otherCategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_EventDoesNotExist_ReturnsNotFound()
    {
        var participant = await Helpers.Participant(_factory);

        var response = await participant.PostAsJsonAsync("/api/events/999999/enrolments", new { categoryId = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_EventAlreadyTookPlace_ReturnsBadRequest()
    {
        var (_, eventId, categoryId) = await NewEventWithCategory();
        await Helpers.MakeEventPast(_factory, eventId);
        var participant = await Helpers.Participant(_factory);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_OnlySeesOwnEnrolments()
    {
        var (_, eventId, categoryId) = await NewEventWithCategory();
        var first = await Helpers.Participant(_factory);
        var second = await Helpers.Participant(_factory);
        await Helpers.Enrol(first, eventId, categoryId);
        await Helpers.Enrol(second, eventId, categoryId);

        var json = await (await first.GetAsync("/api/enrolments/mine")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, json.GetArrayLength());
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForOwnEvent()
    {
        var (organiser, eventId, categoryId) = await NewEventWithCategory();
        var participant = await Helpers.Participant(_factory);
        await Helpers.Enrol(participant, eventId, categoryId);

        var response = await organiser.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetArrayLength());
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_ReturnsForbidden()
    {
        var (_, eventId, _) = await NewEventWithCategory();
        var participant = await Helpers.Participant(_factory);

        var response = await participant.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsOfAnotherOrganisersEvent_ReturnsForbidden()
    {
        var (_, eventId, _) = await NewEventWithCategory();
        var other = await Helpers.Organiser(_factory);

        var response = await other.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanChangeEnrolmentStatus_ParticipantCannot()
    {
        var (organiser, eventId, categoryId) = await NewEventWithCategory();
        var participant = await Helpers.Participant(_factory);
        var enrolmentId = await Helpers.Enrol(participant, eventId, categoryId);

        var asParticipant = await participant.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Pending" });
        var asOrganiser = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Pending" });

        Assert.Equal(HttpStatusCode.Forbidden, asParticipant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asOrganiser.StatusCode);
        var json = await asOrganiser.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", json.GetProperty("status").GetString());
    }
}
