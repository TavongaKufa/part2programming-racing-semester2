using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Api.Tests;

/// <summary>Age / distance categories: Organisers define them, both roles can view them.</summary>
public class CategoryTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public CategoryTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanAddCategoryToOwnEvent()
    {
        var organiser = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "Under 20", categoryType = "Age" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Under 20", json.GetProperty("name").GetString());
        Assert.Equal("Age", json.GetProperty("categoryType").GetString());
    }

    [Fact]
    public async Task Participant_CanViewCategories()
    {
        var organiser = await Helpers.Organiser(_factory);
        var participant = await Helpers.Participant(_factory);
        var eventId = await Helpers.CreateEvent(organiser);
        await Helpers.CreateCategory(organiser, eventId, "10km");
        await Helpers.CreateCategory(organiser, eventId, "21km");

        var response = await participant.GetAsync($"/api/events/{eventId}/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, json.GetArrayLength());
    }

    [Fact]
    public async Task Participant_CannotAddCategory_ReturnsForbidden()
    {
        var organiser = await Helpers.Organiser(_factory);
        var participant = await Helpers.Participant(_factory);
        var eventId = await Helpers.CreateEvent(organiser);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "Sneaky", categoryType = "Age" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotAddCategoryToAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await Helpers.Organiser(_factory);
        var other = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(owner);

        var response = await other.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "Sneaky", categoryType = "Distance" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_DuplicateName_ReturnsConflict()
    {
        var organiser = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(organiser);
        await Helpers.CreateCategory(organiser, eventId, "Senior");

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "Senior", categoryType = "Age" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_EventDoesNotExist_ReturnsNotFound()
    {
        var organiser = await Helpers.Organiser(_factory);

        var response = await organiser.PostAsJsonAsync("/api/events/999999/categories",
            new { name = "Senior", categoryType = "Age" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_InvalidType_ReturnsBadRequest()
    {
        var organiser = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "Weird", categoryType = "Colour" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateAndDeleteCategory()
    {
        var organiser = await Helpers.Organiser(_factory);
        var eventId = await Helpers.CreateEvent(organiser);
        var categoryId = await Helpers.CreateCategory(organiser, eventId, "Old name");

        var update = await organiser.PutAsJsonAsync($"/api/categories/{categoryId}",
            new { name = "New name", categoryType = "Distance" });
        var delete = await organiser.DeleteAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithEnrolments_ReturnsConflict()
    {
        var organiser = await Helpers.Organiser(_factory);
        var participant = await Helpers.Participant(_factory);
        var eventId = await Helpers.CreateEvent(organiser);
        var categoryId = await Helpers.CreateCategory(organiser, eventId);
        await Helpers.Enrol(participant, eventId, categoryId);

        var response = await organiser.DeleteAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
