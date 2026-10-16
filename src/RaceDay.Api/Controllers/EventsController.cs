namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
[Produces("application/json")]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public EventsController(RaceDayDbContext db) => _db = db;

    // ----------------------------- Events -----------------------------

    /// <summary>Lists events, soonest first. Public.</summary>
    /// <param name="eventType">Optional filter: Run, Walk or Cycle.</param>
    /// <param name="status">Optional filter: Upcoming, Completed or Cancelled.</param>
    /// <response code="200">Array of events.</response>
    /// <response code="400">A filter value is invalid.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll([FromQuery] string? eventType = null, [FromQuery] string? status = null)
    {
        var query = _db.Events.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            if (!Canonical.TryMatch(eventType, EventTypes.All, out var type))
                return ValidationError("eventType", "eventType must be Run, Walk or Cycle.");
            query = query.Where(e => e.EventType == type);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Canonical.TryMatch(status, EventStatuses.All, out var st))
                return ValidationError("status", "status must be Upcoming, Completed or Cancelled.");
            query = query.Where(e => e.Status == st);
        }

        var events = await query.OrderBy(e => e.EventDate).ThenBy(e => e.EventId).ToResponse().ToListAsync();
        return Ok(events);
    }

    /// <summary>Returns one event with its name, description, date, location, distance and event type. Public.</summary>
    /// <param name="id">Event id.</param>
    /// <response code="200">The event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events.AsNoTracking().Where(e => e.EventId == id).ToResponse().FirstOrDefaultAsync();
        return ev is null ? NotFoundError("Event") : Ok(ev);
    }

    /// <summary>Creates an event owned by the logged-in Organiser.</summary>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed (e.g. eventType is not Run, Walk or Cycle).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only Organisers can create events.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] EventRequest request)
    {
        var invalid = ValidateEvent(request, out var type, out var status);
        if (invalid != null) return invalid;

        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            EventName = request.EventName.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm!.Value,
            EventType = type,
            Status = string.IsNullOrWhiteSpace(request.Status) ? EventStatuses.Upcoming : status
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var response = await _db.Events.AsNoTracking().Where(e => e.EventId == ev.EventId).ToResponse().SingleAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, response);
    }

    /// <summary>Updates an event (including its status). Only the Organiser who owns it may do this.</summary>
    /// <param name="id">Event id.</param>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] EventRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundError("Event");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenError("You can only modify your own events.");

        var invalid = ValidateEvent(request, out var type, out var status);
        if (invalid != null) return invalid;

        ev.EventName = request.EventName.Trim();
        ev.Description = request.Description?.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm!.Value;
        ev.EventType = type;
        if (!string.IsNullOrWhiteSpace(request.Status)) ev.Status = status;
        await _db.SaveChangesAsync();

        var response = await _db.Events.AsNoTracking().Where(e => e.EventId == id).ToResponse().SingleAsync();
        return Ok(response);
    }

    /// <summary>Deletes an event and its categories. Only the owning Organiser may do this.</summary>
    /// <param name="id">Event id.</param>
    /// <response code="200">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="409">The event already has enrolments, so it cannot be deleted.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundError("Event");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenError("You can only delete your own events.");

        if (await _db.Enrolments.AnyAsync(e => e.Category.EventId == id))
            return ConflictError("This event already has enrolments and cannot be deleted. Set its status to Cancelled instead.");

        _db.Categories.RemoveRange(ev.Categories);
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return Ok(new MessageResponse("Event deleted."));
    }

    // --------------------------- Categories ---------------------------

    /// <summary>Lists the age/distance categories of an event. Public.</summary>
    /// <param name="id">Event id.</param>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategories(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == id)) return NotFoundError("Event");
        var list = await _db.Categories.AsNoTracking().Where(c => c.EventId == id)
            .OrderBy(c => c.CategoryName).ToResponse().ToListAsync();
        return Ok(list);
    }

    /// <summary>Adds an age or distance category (e.g. Under 20, Senior, 10km, 21km) to an event.</summary>
    /// <param name="id">Event id.</param>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="409">A category with this name already exists for the event.</response>
    [HttpPost("{id:int}/categories")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory(int id, [FromBody] CategoryRequest request)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundError("Event");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenError("You can only add categories to your own events.");

        var invalid = ValidateCategory(request, out var type);
        if (invalid != null) return invalid;

        var name = request.CategoryName.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == id && c.CategoryName == name))
            return ConflictError("A category with this name already exists for this event.");

        var category = new Category
        {
            EventId = id,
            CategoryName = name,
            CategoryType = type,
            DistanceKm = type == CategoryTypes.Distance ? request.DistanceKm : null,
            EntryFee = request.EntryFee,
            MaxParticipants = request.MaxParticipants
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var response = await _db.Categories.AsNoTracking().Where(c => c.CategoryId == category.CategoryId).ToResponse().SingleAsync();
        return Created($"/api/events/{id}/categories", response);
    }

    // --------------------------- Enrolments ---------------------------

    /// <summary>Lists all enrolments for an event (all categories). Only the owning Organiser may view them.</summary>
    /// <param name="id">Event id.</param>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owner of this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}/enrolments")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrolments(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundError("Event");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenError("You can only view enrolments for your own events.");

        var list = await _db.Enrolments.AsNoTracking().Where(e => e.Category.EventId == id)
            .OrderBy(e => e.Category.CategoryName).ThenBy(e => e.EnrolmentId)
            .ToResponse().ToListAsync();
        return Ok(list);
    }

    // ----------------------------- Results ----------------------------

    /// <summary>Returns the leaderboard for an event, ordered by finishing position. Public.</summary>
    /// <param name="id">Event id.</param>
    /// <response code="200">Array of results (finished runners first).</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResults(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == id)) return NotFoundError("Event");
        var list = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.Category.EventId == id)
            .OrderBy(r => r.Position == null).ThenBy(r => r.Position).ThenBy(r => r.FinishTime)
            .ToResponse().ToListAsync();
        return Ok(list);
    }

    // ---------------------------- Helpers -----------------------------

    private IActionResult? ValidateEvent(EventRequest r, out string type, out string status)
    {
        status = EventStatuses.Upcoming;
        if (!Canonical.TryMatch(r.EventType, EventTypes.All, out type))
            return ValidationError(nameof(r.EventType), "EventType must be Run, Walk or Cycle.");
        if (!string.IsNullOrWhiteSpace(r.Status) && !Canonical.TryMatch(r.Status, EventStatuses.All, out status))
            return ValidationError(nameof(r.Status), "Status must be Upcoming, Completed or Cancelled.");
        return null;
    }
}
