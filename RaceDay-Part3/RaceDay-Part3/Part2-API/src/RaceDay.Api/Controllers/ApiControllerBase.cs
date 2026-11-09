namespace RaceDay.Api.Controllers;

/// <summary>Shared helpers: current user, consistent error bodies and shared validation rules.</summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user (read from the session-backed principal).</summary>
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected IActionResult ErrorResponse(int status, string title, string? detail = null) =>
        StatusCode(status, new ProblemDetails { Status = status, Title = title, Detail = detail });

    protected IActionResult NotFoundError(string what) => ErrorResponse(404, "Not Found", $"{what} does not exist.");
    protected IActionResult ForbiddenError(string detail) => ErrorResponse(403, "Forbidden", detail);
    protected IActionResult ConflictError(string detail) => ErrorResponse(409, "Conflict", detail);

    protected IActionResult ValidationError(string field, string message) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = new[] { message } }));

    /// <summary>Validates category rules shared by create and update. Returns an error result, or null when valid.</summary>
    protected IActionResult? ValidateCategory(CategoryRequest r, out string type)
    {
        if (!Canonical.TryMatch(r.CategoryType, CategoryTypes.All, out type))
            return ValidationError(nameof(r.CategoryType), "CategoryType must be 'Age' or 'Distance'.");
        if (type == CategoryTypes.Distance && (r.DistanceKm is null || r.DistanceKm <= 0))
            return ValidationError(nameof(r.DistanceKm), "DistanceKm (greater than 0) is required for a Distance category.");
        return null;
    }

    /// <summary>Validates result rules shared by create and update. Returns an error result, or null when valid.</summary>
    protected IActionResult? ValidateResult(ResultRequest r, out string status)
    {
        if (!Canonical.TryMatch(r.Status, ResultStatuses.All, out status))
            return ValidationError(nameof(r.Status), "Status must be Finished, DNF, DNS or Not Started.");
        if (status == ResultStatuses.Finished)
        {
            if (r.FinishTime is null || r.FinishTime <= TimeSpan.Zero)
                return ValidationError(nameof(r.FinishTime), "FinishTime is required for a Finished result.");
            if (r.Position is null)
                return ValidationError(nameof(r.Position), "Position is required for a Finished result.");
        }
        return null;
    }
}
