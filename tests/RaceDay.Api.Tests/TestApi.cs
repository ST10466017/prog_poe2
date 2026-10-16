using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Api.Tests;

/// <summary>Small helpers so each test reads like the scenario it checks.</summary>
public static class TestApi
{
    public const string Password = "Passw0rd!123";

    public static string UniqueEmail(string prefix) => $"{prefix}.{Guid.NewGuid():N}@test.com";

    public static Task<HttpResponseMessage> Register(HttpClient client, string role, string? email = null, string? password = null) =>
        client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Test User",
            email = email ?? UniqueEmail(role.ToLowerInvariant()),
            password = password ?? Password,
            role,
            phone = "0821234567"
        });

    /// <summary>Registers a new user, logs in, and returns a client that carries the session cookie.</summary>
    public static async Task<(HttpClient Client, int UserId, string Email)> LoginAs(RaceDayFactory factory, string role)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail(role.ToLowerInvariant());

        var register = await Register(client, role, email);
        register.EnsureSuccessStatusCode();
        var user = await Json(register);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        return (client, user.GetProperty("userId").GetInt32(), email);
    }

    public static object EventBody(string name = "Test Event", string eventType = "Run", double distanceKm = 10.0, string? status = null) => new
    {
        eventName = name,
        description = "A test event",
        eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
        location = "Cape Town",
        distanceKm,
        eventType,
        status
    };

    public static async Task<int> CreateEvent(HttpClient organiser, string name = "Test Event")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", EventBody(name));
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("eventId").GetInt32();
    }

    public static async Task<int> CreateCategory(HttpClient organiser, int eventId, string name = "10km", int max = 100)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            categoryName = name,
            categoryType = "Distance",
            distanceKm = 10.0,
            entryFee = 150.0,
            maxParticipants = max
        });
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("categoryId").GetInt32();
    }

    public static async Task<int> Enrol(HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsJsonAsync($"/api/categories/{categoryId}/enrol", new { });
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("enrolmentId").GetInt32();
    }

    public static Task<JsonElement> Json(HttpResponseMessage response) => response.Content.ReadFromJsonAsync<JsonElement>();
}
