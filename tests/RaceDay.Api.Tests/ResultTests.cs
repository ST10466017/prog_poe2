using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

/// <summary>Results: Organisers capture finish times/positions; Participants see their own results.</summary>
public class ResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public ResultTests(RaceDayFactory factory) => _factory = factory;

    private static object Finished(string time = "00:52:14", int position = 1) =>
        new { finishTime = time, position, status = "Finished" };

    private async Task<(HttpClient Organiser, HttpClient Participant, int EventId, int EnrolmentId)> Scenario()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var (participant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var eventId = await TestApi.CreateEvent(organiser);
        var categoryId = await TestApi.CreateCategory(organiser, eventId);
        var enrolmentId = await TestApi.Enrol(participant, categoryId);
        return (organiser, participant, eventId, enrolmentId);
    }

    [Fact]
    public async Task Organiser_CanRecordFinishTimeAndPosition()
    {
        var (organiser, _, eventId, enrolmentId) = await Scenario();

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", Finished("00:48:30", 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await TestApi.Json(response);
        Assert.Equal("00:48:30", result.GetProperty("finishTime").GetString());
        Assert.Equal(2, result.GetProperty("position").GetInt32());
        Assert.Equal("Finished", result.GetProperty("status").GetString());
        Assert.Equal(eventId, result.GetProperty("eventId").GetInt32());
    }

    [Fact]
    public async Task Participant_CanViewOwnResult()
    {
        var (organiser, participant, _, enrolmentId) = await Scenario();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", Finished("01:10:00", 5));

        var response = await participant.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await TestApi.Json(response);
        Assert.Equal(1, results.GetArrayLength());
        Assert.Equal(5, results[0].GetProperty("position").GetInt32());
        Assert.Equal("01:10:00", results[0].GetProperty("finishTime").GetString());
    }

    [Fact]
    public async Task Participant_OnlySeesTheirOwnResults_NotOthers()
    {
        var (organiser, participant, eventId, enrolmentId) = await Scenario();
        var (otherParticipant, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var otherCategory = await TestApi.CreateCategory(organiser, eventId, "21km");
        var otherEnrolment = await TestApi.Enrol(otherParticipant, otherCategory);
        // second participant is in a different category of the same event; both get results
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", Finished("00:50:00", 1));
        await organiser.PostAsJsonAsync($"/api/enrolments/{otherEnrolment}/result", Finished("01:40:00", 1));

        var mine = await TestApi.Json(await participant.GetAsync("/api/users/me/results"));
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(enrolmentId, mine[0].GetProperty("enrolmentId").GetInt32());
    }

    [Fact]
    public async Task Leaderboard_IsPublic_AndOrderedByPosition()
    {
        var (organiser, _, eventId, firstEnrolment) = await Scenario();
        var (second, _, _) = await TestApi.LoginAs(_factory, "Participant");
        var secondCategory = await TestApi.CreateCategory(organiser, eventId, "5km");
        var secondEnrolment = await TestApi.Enrol(second, secondCategory);
        await organiser.PostAsJsonAsync($"/api/enrolments/{firstEnrolment}/result", Finished("00:59:00", 2));
        await organiser.PostAsJsonAsync($"/api/enrolments/{secondEnrolment}/result", Finished("00:30:00", 1));

        var response = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = await TestApi.Json(response);
        Assert.Equal(2, board.GetArrayLength());
        Assert.Equal(1, board[0].GetProperty("position").GetInt32());
        Assert.Equal(2, board[1].GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task Leaderboard_ForUnknownEvent_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync("/api/events/999999/results")).StatusCode);
    }

    [Fact]
    public async Task Record_FinishedResult_WithoutTimeOrPosition_Returns400()
    {
        var (organiser, _, _, enrolmentId) = await Scenario();

        var noTime = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { position = 1, status = "Finished" });
        var noPosition = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { finishTime = "00:40:00", status = "Finished" });
        var badStatus = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { finishTime = "00:40:00", position = 1, status = "Won" });

        Assert.Equal(HttpStatusCode.BadRequest, noTime.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noPosition.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
    }

    [Fact]
    public async Task Record_DidNotFinish_DoesNotNeedTimeOrPosition()
    {
        var (organiser, _, _, enrolmentId) = await Scenario();
        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { status = "DNF" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Record_SecondResultForSameEnrolment_Returns409()
    {
        var (organiser, _, _, enrolmentId) = await Scenario();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", Finished());

        var again = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", Finished("00:45:00", 1));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Record_ForUnknownEnrolment_Returns404()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        var response = await organiser.PostAsJsonAsync("/api/enrolments/999999/result", Finished());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotRecordResult_ForAnotherOrganisersEvent_Returns403()
    {
        var (_, _, _, enrolmentId) = await Scenario();
        var (otherOrganiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");

        var response = await otherOrganiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", Finished());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectAResult()
    {
        var (organiser, participant, _, enrolmentId) = await Scenario();
        var created = await TestApi.Json(await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", Finished("00:55:00", 4)));
        var resultId = created.GetProperty("resultId").GetInt32();

        var update = await organiser.PutAsJsonAsync($"/api/results/{resultId}", Finished("00:54:10", 3));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var mine = await TestApi.Json(await participant.GetAsync("/api/users/me/results"));
        Assert.Equal(3, mine[0].GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task Organiser_CannotCorrectAnotherOrganisersResult_Returns403()
    {
        var (organiser, _, _, enrolmentId) = await Scenario();
        var created = await TestApi.Json(await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", Finished()));
        var resultId = created.GetProperty("resultId").GetInt32();
        var (other, _, _) = await TestApi.LoginAs(_factory, "Organiser");

        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/results/{resultId}", Finished("00:01:00", 1))).StatusCode);
    }

    [Fact]
    public async Task Update_UnknownResult_Returns404()
    {
        var (organiser, _, _) = await TestApi.LoginAs(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.PutAsJsonAsync("/api/results/999999", Finished())).StatusCode);
    }
}
