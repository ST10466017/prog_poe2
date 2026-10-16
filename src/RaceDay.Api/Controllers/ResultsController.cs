namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/results")]
[Produces("application/json")]
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Corrects an existing result. Only the Organiser who owns the event may do this.</summary>
    /// <param name="id">Result id.</param>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Result does not exist.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ResultRequest request)
    {
        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFoundError("Result");
        if (result.Enrolment.Category.Event.OrganiserId != CurrentUserId)
            return ForbiddenError("You can only modify results for your own events.");

        var invalid = ValidateResult(request, out var status);
        if (invalid != null) return invalid;

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        result.Status = status;
        result.RecordedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        var response = await _db.Results.AsNoTracking().Where(r => r.ResultId == id).ToResponse().SingleAsync();
        return Ok(response);
    }
}
