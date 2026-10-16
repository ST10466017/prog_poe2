using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

// ------------------------- Requests -------------------------

public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required] public string Role { get; set; } = string.Empty;
    [StringLength(20)] public string? Phone { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = string.Empty;
    [StringLength(20)] public string? Phone { get; set; }
}

public class EventRequest
{
    [Required, StringLength(150)] public string EventName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Required] public DateOnly? EventDate { get; set; }
    [Required, StringLength(150)] public string Location { get; set; } = string.Empty;
    [Required, Range(0.01, 1000.0)] public decimal? DistanceKm { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required] public string EventType { get; set; } = string.Empty;
    /// <summary>Upcoming, Completed or Cancelled. Optional - defaults to Upcoming on create.</summary>
    public string? Status { get; set; }
}

public class CategoryRequest
{
    [Required, StringLength(50)] public string CategoryName { get; set; } = string.Empty;
    /// <summary>Age (e.g. Under 20, Senior) or Distance (e.g. 10km).</summary>
    [Required] public string CategoryType { get; set; } = CategoryTypes.Distance;
    /// <summary>Required (&gt; 0) for Distance categories; ignored for Age categories.</summary>
    [Range(0.01, 1000.0)] public decimal? DistanceKm { get; set; }
    [Range(0, 100000)] public decimal EntryFee { get; set; }
    [Range(1, 100000)] public int MaxParticipants { get; set; } = 100;
}

public class EnrolRequest
{
    /// <summary>Card, EFT or Cash. Optional - defaults to EFT.</summary>
    public string? PaymentMethod { get; set; }
}

public class ResultRequest
{
    /// <summary>Format hh:mm:ss, e.g. 00:52:14.</summary>
    public TimeSpan? FinishTime { get; set; }
    [Range(1, 100000)] public int? Position { get; set; }
    /// <summary>Finished, DNF, DNS or Not Started.</summary>
    [Required] public string Status { get; set; } = ResultStatuses.Finished;
}

// ------------------------- Responses -------------------------

public record MessageResponse(string Message);

public record UserResponse(int UserId, string FullName, string Email, string Role, string? Phone, DateTime CreatedAt)
{
    public static UserResponse From(AppUser u) => new(u.UserId, u.FullName, u.Email, u.Role, u.Phone, u.CreatedAt);
}

public record EventResponse(int EventId, int OrganiserId, string OrganiserName, string EventName, string? Description,
    DateOnly EventDate, string Location, decimal DistanceKm, string EventType, string Status);

public record CategoryResponse(int CategoryId, int EventId, string CategoryName, string CategoryType,
    decimal? DistanceKm, decimal EntryFee, int MaxParticipants);

public record EnrolmentResponse(int EnrolmentId, int ParticipantId, string ParticipantName, int EventId, string EventName,
    int CategoryId, string CategoryName, DateTime EnrolmentDate, string? RaceNumber, string Status, string? PaymentStatus);

public record ResultResponse(int ResultId, int EnrolmentId, int EventId, string EventName, int ParticipantId,
    string ParticipantName, string CategoryName, string? RaceNumber, TimeSpan? FinishTime, int? Position,
    string Status, DateTime RecordedAt);
