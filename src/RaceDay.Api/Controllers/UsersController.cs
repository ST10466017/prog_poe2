namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public UsersController(RaceDayDbContext db) => _db = db;

    /// <summary>Returns the logged-in user's own profile.</summary>
    /// <response code="200">The profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe()
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        return user is null ? NotFoundError("User") : Ok(UserResponse.From(user));
    }

    /// <summary>Updates the logged-in user's own name and phone number.</summary>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return NotFoundError("User");

        user.FullName = request.FullName.Trim();
        user.Phone = request.Phone?.Trim();
        await _db.SaveChangesAsync();
        return Ok(UserResponse.From(user));
    }

    /// <summary>Lists the logged-in Participant's own enrolments.</summary>
    /// <response code="200">Array of enrolments (newest first).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Participants have enrolments.</response>
    [HttpGet("me/enrolments")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MyEnrolments()
    {
        var userId = CurrentUserId;
        var list = await _db.Enrolments.AsNoTracking()
            .Where(e => e.ParticipantId == userId)
            .OrderByDescending(e => e.EnrolmentDate)
            .ToResponse().ToListAsync();
        return Ok(list);
    }

    /// <summary>Lists the logged-in Participant's own results.</summary>
    /// <response code="200">Array of results.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Participants have results.</response>
    [HttpGet("me/results")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MyResults()
    {
        var userId = CurrentUserId;
        var list = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == userId)
            .OrderByDescending(r => r.RecordedAt)
            .ToResponse().ToListAsync();
        return Ok(list);
    }
}
