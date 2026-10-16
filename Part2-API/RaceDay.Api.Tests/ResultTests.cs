using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Api.Tests;

/// <summary>Results: Organisers capture finish times/positions after an event, Participants view their own.</summary>
public class ResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public ResultTests(RaceDayFactory factory) => _factory = factory;

    /// <summary>An event that has finished, with one enrolled Participant per entry in the returned list.</summary>
    private async Task<(HttpClient Organiser, int EventId, List<(HttpClient Client, int EnrolmentId)> Entrants)>
        FinishedEvent(int participants = 1)
    {
        var organiser = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(organiser);
        var categoryId = await Helpers.CreateCategory(organiser, eventId);

        var entrants = new List<(HttpClient, int)>();
        for (var i = 0; i < participants; i++)
        {
            var participant = await Helpers.Participant(_factory);
            entrants.Add((participant, await Helpers.Enrol(participant, eventId, categoryId)));
        }

        await Helpers.MakeEventPast(_factory, eventId);   // the API only allows results once the date has passed
        return (organiser, eventId, entrants);
    }

    [Fact]
    public async Task Organiser_CanCaptureResult()
    {
        var (organiser, eventId, entrants) = await FinishedEvent();

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[0].EnrolmentId, finishTime = "01:52:30", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("01:52:30", json.GetProperty("finishTime").GetString());
        Assert.Equal(1, json.GetProperty("finishPosition").GetInt32());
    }

    [Fact]
    public async Task Participant_CanViewOwnResult_WithPositionAndTotalFinishers()
    {
        var (organiser, eventId, entrants) = await FinishedEvent(participants: 2);
        await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[0].EnrolmentId, finishTime = "00:45:10", finishPosition = 1 });
        await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[1].EnrolmentId, finishTime = "00:51:02", finishPosition = 2 });

        var json = await (await entrants[1].Client.GetAsync("/api/results/mine")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, json.GetArrayLength());
        Assert.Equal(2, json[0].GetProperty("finishPosition").GetInt32());
        Assert.Equal(2, json[0].GetProperty("totalFinishers").GetInt32());
        Assert.Equal("00:51:02", json[0].GetProperty("finishTime").GetString());
    }

    [Fact]
    public async Task EventResults_AreOrderedByPosition_AndVisibleToParticipants()
    {
        var (organiser, eventId, entrants) = await FinishedEvent(participants: 2);
        await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[0].EnrolmentId, finishTime = "00:55:00", finishPosition = 2 });
        await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[1].EnrolmentId, finishTime = "00:50:00", finishPosition = 1 });

        var response = await entrants[0].Client.GetAsync($"/api/events/{eventId}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json[0].GetProperty("finishPosition").GetInt32());
        Assert.Equal(2, json[1].GetProperty("finishPosition").GetInt32());
    }

    [Fact]
    public async Task Participant_CannotCaptureResult_ReturnsForbidden()
    {
        var (_, eventId, entrants) = await FinishedEvent();

        var response = await entrants[0].Client.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[0].EnrolmentId, finishTime = "00:30:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CannotCaptureResult_ReturnsUnauthorized()
    {
        var (_, eventId, entrants) = await FinishedEvent();

        var response = await _factory.NewClient().PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[0].EnrolmentId, finishTime = "00:30:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResultForAnotherOrganisersEvent_ReturnsForbidden()
    {
        var (_, eventId, entrants) = await FinishedEvent();
        var other = await Helpers.Organiser(_factory);

        var response = await other.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[0].EnrolmentId, finishTime = "00:30:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_BeforeEventHasTakenPlace_ReturnsBadRequest()
    {
        var organiser = await Helpers.Organiser(_factory);
        var participant = await Helpers.Participant(_factory);
        var eventId = await Helpers.CreateEvent(organiser);
        var categoryId = await Helpers.CreateCategory(organiser, eventId);
        var enrolmentId = await Helpers.Enrol(participant, eventId, categoryId);

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId, finishTime = "00:30:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12:99:99")]
    [InlineData("00:00:00")]
    public async Task CaptureResult_InvalidFinishTime_ReturnsBadRequest(string finishTime)
    {
        var (organiser, eventId, entrants) = await FinishedEvent();

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[0].EnrolmentId, finishTime, finishPosition = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_ForSameEnrolment_ReturnsConflict()
    {
        var (organiser, eventId, entrants) = await FinishedEvent();
        var body = new { enrolmentId = entrants[0].EnrolmentId, finishTime = "00:30:00", finishPosition = 1 };
        await organiser.PostAsJsonAsync($"/api/events/{eventId}/results", body);

        var second = await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { body.enrolmentId, finishTime = "00:31:00", finishPosition = 2 });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_PositionAlreadyTaken_ReturnsConflict()
    {
        var (organiser, eventId, entrants) = await FinishedEvent(participants: 2);
        await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[0].EnrolmentId, finishTime = "00:30:00", finishPosition = 1 });

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = entrants[1].EnrolmentId, finishTime = "00:31:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_EnrolmentFromAnotherEvent_ReturnsNotFound()
    {
        var (organiser, eventId, _) = await FinishedEvent();
        var (_, _, otherEntrants) = await FinishedEvent();

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { enrolmentId = otherEntrants[0].EnrolmentId, finishTime = "00:30:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewMyResults_ReturnsForbidden()
    {
        var organiser = await Helpers.Organiser(_factory);

        var response = await organiser.GetAsync("/api/results/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
