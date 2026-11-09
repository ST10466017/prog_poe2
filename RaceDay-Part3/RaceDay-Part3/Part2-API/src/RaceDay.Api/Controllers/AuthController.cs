namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public AuthController(RaceDayDbContext db) => _db = db;

    /// <summary>Registers a new account as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Role must be exactly "Organiser" or "Participant" (case-insensitive) - any other role is rejected,
    /// so nobody can register themselves with a made-up or elevated role. The password is hashed before it is stored.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user (never the password).</response>
    /// <response code="400">Validation failed (bad email, password shorter than 8 characters, unknown role...).</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!Canonical.TryMatch(request.Role, Roles.All, out var role))
            return ValidationError(nameof(request.Role), "Role must be 'Organiser' or 'Participant'.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return ConflictError("An account with this email already exists.");

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = role,
            Phone = request.Phone?.Trim()
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created("/api/users/me", UserResponse.From(user));
    }

    /// <summary>Logs in and starts a session that remembers the user and their role.</summary>
    /// <remarks>On success a session cookie is returned; send it back on later requests (Swagger UI does this automatically).</remarks>
    /// <response code="200">Logged in. Returns the user's profile.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return ErrorResponse(401, "Unauthorized", "Invalid email or password.");

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);
        HttpContext.Session.SetString(SessionKeys.Name, user.FullName);

        return Ok(UserResponse.From(user));
    }

    /// <summary>Logs out and ends the current session.</summary>
    /// <response code="200">Session ended.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new MessageResponse("Logged out."));
    }
}
