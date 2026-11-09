using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Auth;

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string Name = "Name";
}

/// <summary>
/// Session-based authentication. Login stores the user id and role in the server-side session
/// (identified by a session cookie). On every request this handler rebuilds the ClaimsPrincipal
/// from the session so [Authorize] and [Authorize(Roles = ...)] work as normal.
/// </summary>
public class SessionAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Session";

    public SessionAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        await Context.Session.LoadAsync();

        var userId = Context.Session.GetInt32(SessionKeys.UserId);
        var role = Context.Session.GetString(SessionKeys.Role);
        if (userId is null || string.IsNullOrEmpty(role))
            return AuthenticateResult.NoResult();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
            new Claim(ClaimTypes.Name, Context.Session.GetString(SessionKeys.Name) ?? string.Empty),
            new Claim(ClaimTypes.Role, role)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    // Not logged in -> 401 (no redirect, this is an API)
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    // Logged in but wrong role -> 403
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
