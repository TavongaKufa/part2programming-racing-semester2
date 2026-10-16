namespace RaceDay.Api.Models;

public enum EventType { Run, Walk, Cycle }

public enum CategoryType { Age, Distance }

// Confirmed is first on purpose: it is the CLR default AND the database default.
public enum EnrolmentStatus { Confirmed, Pending }

public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = { Organiser, Participant };
}

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public List<User> Users { get; set; } = new();
}

public class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Event> OrganisedEvents { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public User Organiser { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public EventType EventType { get; set; }
    public string? BannerImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Category> Categories { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Name { get; set; } = "";
    public CategoryType CategoryType { get; set; }

    public List<Enrolment> Enrolments { get; set; } = new();
}

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Confirmed;

    public RaceResult? Result { get; set; }
}

public class RaceResult
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment Enrolment { get; set; } = null!;
    public TimeSpan FinishTime { get; set; }
    public int FinishPosition { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
