namespace RaceDay.Api.Extensions;

/// <summary>Reusable projections from entities to response DTOs.</summary>
public static class QueryExtensions
{
    public static IQueryable<EventResponse> ToResponse(this IQueryable<Event> q) =>
        q.Select(e => new EventResponse(e.EventId, e.OrganiserId, e.Organiser.FullName, e.EventName, e.Description,
            e.EventDate, e.Location, e.DistanceKm, e.EventType, e.Status));

    public static IQueryable<CategoryResponse> ToResponse(this IQueryable<Category> q) =>
        q.Select(c => new CategoryResponse(c.CategoryId, c.EventId, c.CategoryName, c.CategoryType,
            c.DistanceKm, c.EntryFee, c.MaxParticipants));

    public static IQueryable<EnrolmentResponse> ToResponse(this IQueryable<Enrolment> q) =>
        q.Select(e => new EnrolmentResponse(e.EnrolmentId, e.ParticipantId, e.Participant.FullName,
            e.Category.EventId, e.Category.Event.EventName, e.CategoryId, e.Category.CategoryName,
            e.EnrolmentDate, e.RaceNumber, e.Status, e.Payment != null ? e.Payment.PaymentStatus : null));

    public static IQueryable<ResultResponse> ToResponse(this IQueryable<Result> q) =>
        q.Select(r => new ResultResponse(r.ResultId, r.EnrolmentId, r.Enrolment.Category.EventId,
            r.Enrolment.Category.Event.EventName, r.Enrolment.ParticipantId, r.Enrolment.Participant.FullName,
            r.Enrolment.Category.CategoryName, r.Enrolment.RaceNumber, r.FinishTime, r.Position, r.Status, r.RecordedAt));
}
