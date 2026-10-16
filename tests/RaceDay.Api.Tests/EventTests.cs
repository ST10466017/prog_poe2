using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

/// <summary>Event and category management by Organisers, and public viewing.</summary>
public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public EventTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanCreateEvent_WithAllRequiredFields()
    {
        var (organiser, userId, _) = await TestApi.LoginAs(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", TestApi.EventBody("City Cycle", "Cycle", 40.5));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var ev = await TestApi.Json(response);
        Assert.Equal("City Cycle", ev.GetProperty("eventName").GetString());
        Assert.Equal("A test event", ev.GetProperty("description").GetString());
        Assert.Equal("Cape Town", ev.GetProperty("location").GetString());
        Assert.Equal(40.5m, ev.GetProperty("distanceKm").GetDecimal());
        Assert.Equal("Cycle", ev.GetProperty("eventType").GetString());
        Assert.Equal("Upcoming", ev.GetProperty("status").GetString());
        Assert.Equal(userId, ev.GetProperty("organiserId").GetInt32());
    }

    [Theory]
    [InlineData("Run")]
    [InlineData("Walk")]
    [InlineData("Cycle")]
    [InlineData("cycle")]   // case-insensitive
    public async Task Create_AcceptsAllEventTypes(string eventType)
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events", TestApi.EventBody(eventType: eventType));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidEventType_Returns400()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events", TestApi.EventBody(eventType: "Swim"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithMissingName_Returns400()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events", TestApi.EventBody(name: ""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithZeroDistance_Returns400()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events", TestApi.EventBody(distanceKm: 0));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Events_CanBeViewedWithoutLogin()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var id = await TestApi.CreateEvent(organiser, "Public Event");
        var anonymous = _factory.CreateClient();

        var list = await anonymous.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("Public Event", await list.Content.ReadAsStringAsync());

        var one = await anonymous.GetAsync($"/api/events/{id}");
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
    }

    [Fact]
    public async Task Events_CanBeViewedByParticipants()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var id = await TestApi.CreateEvent(organiser);
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");

        Assert.Equal(HttpStatusCode.OK, (await participant.GetAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task GetEvent_Unknown_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_CanBeFiltered_ByEventType()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        await organiser.PostAsJsonAsync("/api/events", TestApi.EventBody("Filter Walk Event", "Walk"));
        await organiser.PostAsJsonAsync("/api/events", TestApi.EventBody("Filter Run Event", "Run"));

        var body = await (await _factory.CreateClient().GetAsync("/api/events?eventType=Walk")).Content.ReadAsStringAsync();
        Assert.Contains("Filter Walk Event", body);
        Assert.DoesNotContain("Filter Run Event", body);

        var bad = await _factory.CreateClient().GetAsync("/api/events?eventType=Swim");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var id = await TestApi.CreateEvent(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{id}", TestApi.EventBody("Renamed Event", "Walk", 5, "Completed"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ev = await TestApi.Json(response);
        Assert.Equal("Renamed Event", ev.GetProperty("eventName").GetString());
        Assert.Equal("Walk", ev.GetProperty("eventType").GetString());
        Assert.Equal("Completed", ev.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var (owner, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (other, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var id = await TestApi.CreateEvent(owner);

        var response = await other.PutAsJsonAsync($"/api/events/{id}", TestApi.EventBody("Hijacked"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownEvent_Returns404()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var response = await organiser.PutAsJsonAsync("/api/events/999999", TestApi.EventBody());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_ThenItIsGone()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var id = await TestApi.CreateEvent(organiser);
        await TestApi.CreateCategory(organiser, id);

        Assert.Equal(HttpStatusCode.OK, (await organiser.DeleteAsync($"/api/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var (owner, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (other, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var id = await TestApi.CreateEvent(owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_EventWithEnrolments_Returns409()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var eventId = await TestApi.CreateEvent(organiser);
        var categoryId = await TestApi.CreateCategory(organiser, eventId);
        await TestApi.Enrol(participant, categoryId);

        Assert.Equal(HttpStatusCode.Conflict, (await organiser.DeleteAsync($"/api/events/{eventId}")).StatusCode);
    }

    // ------------------------------ categories ------------------------------

    [Fact]
    public async Task Organiser_CanCreateAgeAndDistanceCategories_AndEveryoneCanViewThem()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var eventId = await TestApi.CreateEvent(organiser);

        var age = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = "Under 20", categoryType = "Age", entryFee = 80, maxParticipants = 50 });
        var distance = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = "21km", categoryType = "Distance", distanceKm = 21.1, entryFee = 200, maxParticipants = 50 });

        Assert.Equal(HttpStatusCode.Created, age.StatusCode);
        Assert.Equal(HttpStatusCode.Created, distance.StatusCode);

        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        foreach (var viewer in new[] { _factory.CreateClient(), participant })
        {
            var list = await viewer.GetAsync($"/api/events/{eventId}/categories");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var body = await list.Content.ReadAsStringAsync();
            Assert.Contains("Under 20", body);
            Assert.Contains("21km", body);
        }
    }

    [Fact]
    public async Task Create_DistanceCategory_WithoutDistance_Returns400()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var eventId = await TestApi.CreateEvent(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = "10km", categoryType = "Distance", entryFee = 100, maxParticipants = 50 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateCategoryName_Returns409()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var eventId = await TestApi.CreateEvent(organiser);
        await TestApi.CreateCategory(organiser, eventId, "10km");

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = "10km", categoryType = "Distance", distanceKm = 10, entryFee = 100, maxParticipants = 50 });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateAndDeleteOwnCategory()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var eventId = await TestApi.CreateEvent(organiser);
        var categoryId = await TestApi.CreateCategory(organiser, eventId);

        var update = await organiser.PutAsJsonAsync($"/api/categories/{categoryId}",
            new { categoryName = "Senior", categoryType = "Age", entryFee = 120, maxParticipants = 75 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Senior", (await TestApi.Json(update)).GetProperty("categoryName").GetString());

        Assert.Equal(HttpStatusCode.OK, (await organiser.DeleteAsync($"/api/categories/{categoryId}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotModifyAnotherOrganisersCategory_Returns403()
    {
        var (owner, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (other, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var eventId = await TestApi.CreateEvent(owner);
        var categoryId = await TestApi.CreateCategory(owner, eventId);

        var update = await other.PutAsJsonAsync($"/api/categories/{categoryId}",
            new { categoryName = "Hacked", categoryType = "Age", entryFee = 1, maxParticipants = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/categories/{categoryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await other.PostAsJsonAsync($"/api/events/{eventId}/categories",
                new { categoryName = "Extra", categoryType = "Age", entryFee = 1, maxParticipants = 1 })).StatusCode);
    }

    [Fact]
    public async Task Delete_CategoryWithEnrolments_Returns409()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var eventId = await TestApi.CreateEvent(organiser);
        var categoryId = await TestApi.CreateCategory(organiser, eventId);
        await TestApi.Enrol(participant, categoryId);

        Assert.Equal(HttpStatusCode.Conflict, (await organiser.DeleteAsync($"/api/categories/{categoryId}")).StatusCode);
    }

    [Fact]
    public async Task GetCategories_ForUnknownEvent_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync("/api/events/999999/categories")).StatusCode);
    }
}
