namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/enrolments")]
[Produces("application/json")]
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Cancels the logged-in Participant's own enrolment (before the event date).</summary>
    /// <remarks>The enrolment is kept with status Cancelled; a paid payment is marked Refunded.</remarks>
    /// <param name="id">Enrolment id.</param>
    /// <response code="200">Enrolment cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant, or not your enrolment.</response>
    /// <response code="404">Enrolment does not exist.</response>
    /// <response code="409">Already cancelled, the event date has passed, or a result is already recorded.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Include(e => e.Payment).Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFoundError("Enrolment");
        if (enrolment.ParticipantId != CurrentUserId) return ForbiddenError("You can only cancel your own enrolments.");

        if (enrolment.Status == EnrolmentStatuses.Cancelled) return ConflictError("This enrolment is already cancelled.");
        if (enrolment.Result is not null) return ConflictError("A result is already recorded for this enrolment.");
        if (enrolment.Category.Event.EventDate < DateOnly.FromDateTime(DateTime.Today))
            return ConflictError("The event date has passed, so the enrolment can no longer be cancelled.");

        enrolment.Status = EnrolmentStatuses.Cancelled;
        if (enrolment.Payment is { PaymentStatus: PaymentStatuses.Paid }) enrolment.Payment.PaymentStatus = PaymentStatuses.Refunded;
        await _db.SaveChangesAsync();
        return Ok(new MessageResponse("Enrolment cancelled."));
    }

    /// <summary>Records the finish time and finishing position for a Participant's enrolment.</summary>
    /// <remarks>Only the Organiser who owns the event may do this. A Finished result needs finishTime and position.</remarks>
    /// <param name="id">Enrolment id.</param>
    /// <response code="201">Result recorded.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Enrolment does not exist.</response>
    /// <response code="409">A result already exists (use PUT /api/results/{id}), or the enrolment is cancelled.</response>
    [HttpPost("{id:int}/result")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordResult(int id, [FromBody] ResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFoundError("Enrolment");
        if (enrolment.Category.Event.OrganiserId != CurrentUserId)
            return ForbiddenError("You can only record results for your own events.");

        var invalid = ValidateResult(request, out var status);
        if (invalid != null) return invalid;

        if (enrolment.Status == EnrolmentStatuses.Cancelled) return ConflictError("This enrolment is cancelled.");
        if (enrolment.Result is not null) return ConflictError("A result already exists for this enrolment. Use PUT /api/results/{id} to correct it.");

        var result = new Result { EnrolmentId = id, FinishTime = request.FinishTime, Position = request.Position, Status = status };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var response = await _db.Results.AsNoTracking().Where(r => r.ResultId == result.ResultId).ToResponse().SingleAsync();
        return Created($"/api/events/{enrolment.Category.EventId}/results", response);
    }
}
