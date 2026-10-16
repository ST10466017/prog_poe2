namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>Updates a category. Only the Organiser who owns the event may do this.</summary>
    /// <param name="id">Category id.</param>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">Duplicate name for this event, or the limit is below the current enrolments.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundError("Category");
        if (category.Event.OrganiserId != CurrentUserId) return ForbiddenError("You can only modify categories of your own events.");

        var invalid = ValidateCategory(request, out var type);
        if (invalid != null) return invalid;

        var name = request.CategoryName.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == category.EventId && c.CategoryName == name && c.CategoryId != id))
            return ConflictError("A category with this name already exists for this event.");

        var active = await _db.Enrolments.CountAsync(e => e.CategoryId == id && e.Status != EnrolmentStatuses.Cancelled);
        if (request.MaxParticipants < active)
            return ConflictError($"MaxParticipants cannot be lower than the {active} current enrolment(s).");

        category.CategoryName = name;
        category.CategoryType = type;
        category.DistanceKm = type == CategoryTypes.Distance ? request.DistanceKm : null;
        category.EntryFee = request.EntryFee;
        category.MaxParticipants = request.MaxParticipants;
        await _db.SaveChangesAsync();

        var response = await _db.Categories.AsNoTracking().Where(c => c.CategoryId == id).ToResponse().SingleAsync();
        return Ok(response);
    }

    /// <summary>Removes a category. Only the owning Organiser may do this.</summary>
    /// <param name="id">Category id.</param>
    /// <response code="200">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">The category already has enrolments.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundError("Category");
        if (category.Event.OrganiserId != CurrentUserId) return ForbiddenError("You can only delete categories of your own events.");

        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return ConflictError("This category already has enrolments and cannot be deleted.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return Ok(new MessageResponse("Category deleted."));
    }

    /// <summary>
    /// Enrols the logged-in Participant in a category. Records the link between the Participant, the event and the
    /// category, assigns a race number and creates a pending payment for the entry fee.
    /// </summary>
    /// <remarks>A Participant can be enrolled in only one category per event. The body is optional.</remarks>
    /// <param name="id">Category id.</param>
    /// <response code="201">Enrolment created.</response>
    /// <response code="400">Invalid payment method.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Participants can enrol.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">Already enrolled in this event, category is full, or the event is not open for enrolment.</response>
    [HttpPost("{id:int}/enrol")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EnrolRequest? request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundError("Category");
        if (category.Event.Status != EventStatuses.Upcoming)
            return ConflictError("This event is not open for enrolment.");

        var method = PaymentMethods.Eft;
        if (!string.IsNullOrWhiteSpace(request?.PaymentMethod) && !Canonical.TryMatch(request.PaymentMethod, PaymentMethods.All, out method))
            return ValidationError(nameof(EnrolRequest.PaymentMethod), "PaymentMethod must be Card, EFT or Cash.");

        var userId = CurrentUserId;
        var eventId = category.EventId;

        var existing = await _db.Enrolments.Include(e => e.Payment)
            .FirstOrDefaultAsync(e => e.CategoryId == id && e.ParticipantId == userId);
        if (existing is not null && existing.Status != EnrolmentStatuses.Cancelled)
            return ConflictError("You are already enrolled in this category.");

        var otherInEvent = await _db.Enrolments.AnyAsync(e => e.ParticipantId == userId && e.Category.EventId == eventId
            && e.CategoryId != id && e.Status != EnrolmentStatuses.Cancelled);
        if (otherInEvent)
            return ConflictError("You are already enrolled in another category of this event.");

        var taken = await _db.Enrolments.CountAsync(e => e.CategoryId == id && e.Status != EnrolmentStatuses.Cancelled);
        if (taken >= category.MaxParticipants)
            return ConflictError("This category is full.");

        Enrolment enrolment;
        if (existing is null)
        {
            enrolment = new Enrolment { ParticipantId = userId, CategoryId = id, Status = EnrolmentStatuses.Confirmed };
            _db.Enrolments.Add(enrolment);
        }
        else
        {
            // Re-enrolling after a cancellation re-activates the old row (participant + category must stay unique).
            enrolment = existing;
            enrolment.Status = EnrolmentStatuses.Confirmed;
            enrolment.EnrolmentDate = DateTime.Now;
        }

        if (enrolment.Payment is null)
        {
            enrolment.Payment = new Payment { Amount = category.EntryFee, PaymentMethod = method, PaymentStatus = PaymentStatuses.Pending };
        }
        else
        {
            enrolment.Payment.Amount = category.EntryFee;
            enrolment.Payment.PaymentMethod = method;
            enrolment.Payment.PaymentStatus = PaymentStatuses.Pending;
            enrolment.Payment.PaidAt = null;
        }
        await _db.SaveChangesAsync();

        if (string.IsNullOrEmpty(enrolment.RaceNumber))
        {
            enrolment.RaceNumber = $"E{eventId:D3}-{enrolment.EnrolmentId:D4}";
            await _db.SaveChangesAsync();
        }

        var response = await _db.Enrolments.AsNoTracking().Where(e => e.EnrolmentId == enrolment.EnrolmentId).ToResponse().SingleAsync();
        return Created("/api/users/me/enrolments", response);
    }

    /// <summary>Lists the participants enrolled in one category. Only the owning Organiser may view them.</summary>
    /// <param name="id">Category id.</param>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of the event.</response>
    /// <response code="404">Category does not exist.</response>
    [HttpGet("{id:int}/enrolments")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrolments(int id)
    {
        var category = await _db.Categories.AsNoTracking().Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundError("Category");
        if (category.Event.OrganiserId != CurrentUserId) return ForbiddenError("You can only view enrolments for your own events.");

        var list = await _db.Enrolments.AsNoTracking().Where(e => e.CategoryId == id)
            .OrderBy(e => e.EnrolmentId).ToResponse().ToListAsync();
        return Ok(list);
    }
}
