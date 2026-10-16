using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

/// <summary>
/// Authenticated vs unauthenticated requests, and role-based access:
/// no login -> 401, wrong role -> 403, right role -> allowed.
/// </summary>
public class RoleAccessTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public RoleAccessTests(RaceDayFactory factory) => _factory = factory;

    // ----------------------- unauthenticated -> 401 -----------------------

    [Fact]
    public async Task Anonymous_IsRejected_OnEveryProtectedEndpoint()
    {
        var anonymous = _factory.CreateClient();
        static JsonContent Body() => JsonContent.Create(new { });

        var responses = new[]
        {
            await anonymous.GetAsync("/api/users/me"),
            await anonymous.PutAsync("/api/users/me", JsonContent.Create(new { fullName = "X Y" })),
            await anonymous.PostAsync("/api/auth/logout", null),
            await anonymous.PostAsync("/api/events", JsonContent.Create(TestApi.EventBody())),
            await anonymous.PutAsync("/api/events/1", JsonContent.Create(TestApi.EventBody())),
            await anonymous.DeleteAsync("/api/events/1"),
            await anonymous.PostAsync("/api/events/1/categories", Body()),
            await anonymous.PutAsync("/api/categories/1", Body()),
            await anonymous.DeleteAsync("/api/categories/1"),
            await anonymous.PostAsync("/api/categories/1/enrol", Body()),
            await anonymous.GetAsync("/api/users/me/enrolments"),
            await anonymous.GetAsync("/api/users/me/results"),
            await anonymous.GetAsync("/api/events/1/enrolments"),
            await anonymous.GetAsync("/api/categories/1/enrolments"),
            await anonymous.DeleteAsync("/api/enrolments/1"),
            await anonymous.PostAsync("/api/enrolments/1/result", Body()),
            await anonymous.PutAsync("/api/results/1", Body()),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    [Fact]
    public async Task Anonymous_CanUsePublicEndpoints()
    {
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/events")).StatusCode);
        // register is public too: no session needed
        Assert.Equal(HttpStatusCode.Created, (await TestApi.Register(anonymous, "Participant")).StatusCode);
    }

    // ----------------- Participant using Organiser features -> 403 -----------------

    [Fact]
    public async Task Participant_CannotUseOrganiserEndpoints_Returns403()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var eventId = await TestApi.CreateEvent(organiser);
        var categoryId = await TestApi.CreateCategory(organiser, eventId);
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var enrolmentId = await TestApi.Enrol(participant, categoryId);
        static JsonContent Body() => JsonContent.Create(new { categoryName = "X", categoryType = "Age", entryFee = 1, maxParticipants = 1 });

        var responses = new[]
        {
            await participant.PostAsJsonAsync("/api/events", TestApi.EventBody()),
            await participant.PutAsJsonAsync($"/api/events/{eventId}", TestApi.EventBody()),
            await participant.DeleteAsync($"/api/events/{eventId}"),
            await participant.PostAsync($"/api/events/{eventId}/categories", Body()),
            await participant.PutAsync($"/api/categories/{categoryId}", Body()),
            await participant.DeleteAsync($"/api/categories/{categoryId}"),
            await participant.GetAsync($"/api/events/{eventId}/enrolments"),
            await participant.GetAsync($"/api/categories/{categoryId}/enrolments"),
            await participant.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result",
                new { finishTime = "00:50:00", position = 1, status = "Finished" }),
            await participant.PutAsJsonAsync("/api/results/1", new { finishTime = "00:50:00", position = 1, status = "Finished" }),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    // ----------------- Organiser using Participant features -> 403 -----------------

    [Fact]
    public async Task Organiser_CannotUseParticipantEndpoints_Returns403()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var eventId = await TestApi.CreateEvent(organiser);
        var categoryId = await TestApi.CreateCategory(organiser, eventId);

        var responses = new[]
        {
            await organiser.PostAsJsonAsync($"/api/categories/{categoryId}/enrol", new { }),
            await organiser.GetAsync("/api/users/me/enrolments"),
            await organiser.GetAsync("/api/users/me/results"),
            await organiser.DeleteAsync("/api/enrolments/1"),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    [Fact]
    public async Task Organiser_CannotEnrol_AndNoEnrolmentIsCreated()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var eventId = await TestApi.CreateEvent(organiser);
        var categoryId = await TestApi.CreateCategory(organiser, eventId);

        await organiser.PostAsJsonAsync($"/api/categories/{categoryId}/enrol", new { });

        var enrolments = await TestApi.Json(await organiser.GetAsync($"/api/events/{eventId}/enrolments"));
        Assert.Equal(0, enrolments.GetArrayLength());
    }

    [Fact]
    public async Task Role_CannotBeChanged_ThroughProfileUpdate()
    {
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");

        await participant.PutAsJsonAsync("/api/users/me", new { fullName = "Sneaky", phone = "0821112222", role = "Organiser" });

        var me = await TestApi.Json(await participant.GetAsync("/api/users/me"));
        Assert.Equal("Participant", me.GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.PostAsJsonAsync("/api/events", TestApi.EventBody())).StatusCode);
    }

    [Fact]
    public async Task AfterLogout_ProtectedEndpointsAreRejectedAgain()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.Created, (await organiser.PostAsJsonAsync("/api/events", TestApi.EventBody())).StatusCode);

        await organiser.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await organiser.PostAsJsonAsync("/api/events", TestApi.EventBody())).StatusCode);
    }
}
