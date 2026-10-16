namespace RaceDay.Api.Models;

/// <summary>Maps to the Users table (Organisers and Participants, distinguished by Role).</summary>
public class AppUser
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Participant;
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<Event> OrganisedEvents { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DistanceKm { get; set; }
    public string EventType { get; set; } = "Run";
    public string Status { get; set; } = EventStatuses.Upcoming;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public AppUser Organiser { get; set; } = null!;
    public List<Category> Categories { get; set; } = new();
}

public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryType { get; set; } = CategoryTypes.Distance;
    public decimal? DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; } = 100;

    public Event Event { get; set; } = null!;
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.Now;
    public string? RaceNumber { get; set; }
    public string Status { get; set; } = EnrolmentStatuses.Confirmed;

    public AppUser Participant { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Result? Result { get; set; }
    public Payment? Payment { get; set; }
}

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = ResultStatuses.NotStarted;
    public DateTime RecordedAt { get; set; } = DateTime.Now;

    public Enrolment Enrolment { get; set; } = null!;
}

public class Payment
{
    public int PaymentId { get; set; }
    public int EnrolmentId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Eft;
    public string PaymentStatus { get; set; } = PaymentStatuses.Pending;
    public DateTime? PaidAt { get; set; }

    public Enrolment Enrolment { get; set; } = null!;
}
