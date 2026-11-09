namespace RaceDay.Api.Models;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = { Organiser, Participant };
}

public static class EventTypes
{
    public static readonly string[] All = { "Run", "Walk", "Cycle" };
}

public static class EventStatuses
{
    public const string Upcoming = "Upcoming";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = { Upcoming, Completed, Cancelled };
}

public static class CategoryTypes
{
    public const string Age = "Age";
    public const string Distance = "Distance";
    public static readonly string[] All = { Age, Distance };
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

public static class ResultStatuses
{
    public const string NotStarted = "Not Started";
    public const string Dns = "DNS";
    public const string Dnf = "DNF";
    public const string Finished = "Finished";
    public static readonly string[] All = { NotStarted, Dns, Dnf, Finished };
}

public static class PaymentMethods
{
    public const string Card = "Card";
    public const string Eft = "EFT";
    public const string Cash = "Cash";
    public static readonly string[] All = { Card, Eft, Cash };
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Refunded = "Refunded";
}
