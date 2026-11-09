using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace RaceDay.Api.Tests;

/// <summary>Event enrolments: Participants enrol by category; the link is recorded; Organisers can view it.</summary>
public class EnrolmentTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public EnrolmentTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Participant_CanEnrol_AndTheEnrolmentIsCorrectlyRecorded()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (participant, participantId, _) = await TestApi.LoginAs(_factory, "Participant");
        var eventId = await TestApi.CreateEvent(organiser, "Recorded Event");
        var categoryId = await TestApi.CreateCategory(organiser, eventId, "10km");

        var response = await participant.PostAsJsonAsync($"/api/categories/{categoryId}/enrol", new { paymentMethod = "Card" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await TestApi.Json(response);
        Assert.Equal(participantId, enrolment.GetProperty("participantId").GetInt32());
        Assert.Equal(eventId, enrolment.GetProperty("eventId").GetInt32());
        Assert.Equal(categoryId, enrolment.GetProperty("categoryId").GetInt32());
        Assert.Equal("Recorded Event", enrolment.GetProperty("eventName").GetString());
        Assert.Equal("10km", enrolment.GetProperty("categoryName").GetString());
        Assert.Equal("Confirmed", enrolment.GetProperty("status").GetString());
        Assert.Equal("Pending", enrolment.GetProperty("paymentStatus").GetString());
        Assert.False(string.IsNullOrWhiteSpace(enrolment.GetProperty("raceNumber").GetString()));

        // it shows up in the participant's own list...
        var mine = await TestApi.Json(await participant.GetAsync("/api/users/me/enrolments"));
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(categoryId, mine[0].GetProperty("categoryId").GetInt32());
    }

    [Fact]
    public async Task Enrol_WithoutBody_DefaultsPaymentMethod_AndSucceeds()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var categoryId = await TestApi.CreateCategory(organiser, await TestApi.CreateEvent(organiser));

        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrol",
            new StringContent("", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithInvalidPaymentMethod_Returns400()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var categoryId = await TestApi.CreateCategory(organiser, await TestApi.CreateEvent(organiser));

        var response = await participant.PostAsJsonAsync($"/api/categories/{categoryId}/enrol", new { paymentMethod = "Bitcoin" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewAllEnrolments_ForTheirEvent_AndPerCategory()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (emma, emmaId, _) = await TestApi.LoginAs(_factory, "Participant");
        var (liam, liamId, _) = await TestApi.LoginAs(_factory, "Participant");
        var eventId = await TestApi.CreateEvent(organiser);
        var tenK = await TestApi.CreateCategory(organiser, eventId, "10km");
        var twentyOneK = await TestApi.CreateCategory(organiser, eventId, "21km");
        await TestApi.Enrol(emma, tenK);
        await TestApi.Enrol(liam, twentyOneK);

        var all = await TestApi.Json(await organiser.GetAsync($"/api/events/{eventId}/enrolments"));
        Assert.Equal(2, all.GetArrayLength());
        var participantIds = all.EnumerateArray().Select(e => e.GetProperty("participantId").GetInt32()).ToList();
        Assert.Contains(emmaId, participantIds);
        Assert.Contains(liamId, participantIds);

        var onlyTenK = await TestApi.Json(await organiser.GetAsync($"/api/categories/{tenK}/enrolments"));
        Assert.Equal(1, onlyTenK.GetArrayLength());
        Assert.Equal(emmaId, onlyTenK[0].GetProperty("participantId").GetInt32());
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolments_OfAnotherOrganisersEvent()
    {
        var (owner, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (other, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var eventId = await TestApi.CreateEvent(owner);
        var categoryId = await TestApi.CreateCategory(owner, eventId);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/events/{eventId}/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/categories/{categoryId}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Enrol_InSameCategoryTwice_Returns409()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var categoryId = await TestApi.CreateCategory(organiser, await TestApi.CreateEvent(organiser));
        await TestApi.Enrol(participant, categoryId);

        var again = await participant.PostAsJsonAsync($"/api/categories/{categoryId}/enrol", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Enrol_InTwoCategoriesOfTheSameEvent_Returns409()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var eventId = await TestApi.CreateEvent(organiser);
        var tenK = await TestApi.CreateCategory(organiser, eventId, "10km");
        var twentyOneK = await TestApi.CreateCategory(organiser, eventId, "21km");
        await TestApi.Enrol(participant, tenK);

        var second = await participant.PostAsJsonAsync($"/api/categories/{twentyOneK}/enrol", new { });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_InUnknownCategory_Returns404()
    {
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var response = await participant.PostAsJsonAsync("/api/categories/999999/enrol", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenCategoryIsFull_Returns409()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (first, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var (second, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var categoryId = await TestApi.CreateCategory(organiser, await TestApi.CreateEvent(organiser), "Tiny", max: 1);

        await TestApi.Enrol(first, categoryId);
        var full = await second.PostAsJsonAsync($"/api/categories/{categoryId}/enrol", new { });

        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenEventIsCancelled_Returns409()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var eventId = await TestApi.CreateEvent(organiser);
        var categoryId = await TestApi.CreateCategory(organiser, eventId);
        await organiser.PutAsJsonAsync($"/api/events/{eventId}", TestApi.EventBody(status: "Cancelled"));

        var response = await participant.PostAsJsonAsync($"/api/categories/{categoryId}/enrol", new { });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_AndFreeUpTheSpot_ThenReEnrol()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (first, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var (second, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var categoryId = await TestApi.CreateCategory(organiser, await TestApi.CreateEvent(organiser), "Tiny", max: 1);
        var enrolmentId = await TestApi.Enrol(first, categoryId);

        Assert.Equal(HttpStatusCode.OK, (await first.DeleteAsync($"/api/enrolments/{enrolmentId}")).StatusCode);

        var mine = await TestApi.Json(await first.GetAsync("/api/users/me/enrolments"));
        Assert.Equal("Cancelled", mine[0].GetProperty("status").GetString());

        // the spot is free again for someone else
        Assert.Equal(HttpStatusCode.Created, (await second.PostAsJsonAsync($"/api/categories/{categoryId}/enrol", new { })).StatusCode);
        // cancelling twice is rejected
        Assert.Equal(HttpStatusCode.Conflict, (await first.DeleteAsync($"/api/enrolments/{enrolmentId}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCancelSomeoneElsesEnrolment_Returns403()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (owner, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var (stranger, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var categoryId = await TestApi.CreateCategory(organiser, await TestApi.CreateEvent(organiser));
        var enrolmentId = await TestApi.Enrol(owner, categoryId);

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.DeleteAsync($"/api/enrolments/{enrolmentId}")).StatusCode);
    }

    [Fact]
    public async Task Cancel_UnknownEnrolment_Returns404()
    {
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        Assert.Equal(HttpStatusCode.NotFound, (await participant.DeleteAsync("/api/enrolments/999999")).StatusCode);
    }
}
