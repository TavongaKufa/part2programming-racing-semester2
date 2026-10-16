using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

// ---------------------------------------------------------------- Auth / Profile
public class RegisterRequest
{
    [Required, StringLength(50)] public string FirstName { get; set; } = "";
    [Required, StringLength(50)] public string LastName { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";
    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required] public string Role { get; set; } = "";
    [StringLength(20)] public string? PhoneNumber { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class UpdateProfileRequest
{
    [Required, StringLength(50)] public string FirstName { get; set; } = "";
    [Required, StringLength(50)] public string LastName { get; set; } = "";
    [StringLength(20)] public string? PhoneNumber { get; set; }
}

public record ProfileResponse(
    int UserId, string FirstName, string LastName, string Email,
    string Role, string? PhoneNumber, string? ProfilePictureUrl)
{
    public static ProfileResponse From(User u) =>
        new(u.UserId, u.FirstName, u.LastName, u.Email, u.Role.RoleName, u.PhoneNumber, u.ProfilePictureUrl);
}

// ---------------------------------------------------------------- Events
public class EventRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, StringLength(1000)] public string Description { get; set; } = "";
    [Required] public DateTime? EventDate { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = "";
    /// <summary>Distance in kilometres (e.g. 21.1).</summary>
    [Range(0.1, 1000)] public decimal DistanceKm { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required] public EventType? EventType { get; set; }
}

public record EventResponse(
    int EventId, string Name, string Description, DateTime EventDate, string Location,
    decimal DistanceKm, EventType EventType, int OrganiserId, string OrganiserName, int EnrolmentCount);

// ---------------------------------------------------------------- Categories
public class CategoryRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    /// <summary>Age or Distance.</summary>
    [Required] public CategoryType? CategoryType { get; set; }
}

public record CategoryResponse(int CategoryId, int EventId, string Name, CategoryType CategoryType);

// ---------------------------------------------------------------- Enrolments
public class EnrolRequest
{
    /// <summary>The category (belonging to the event) the participant is entering.</summary>
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
}

public class UpdateEnrolmentStatusRequest
{
    /// <summary>Confirmed or Pending.</summary>
    [Required] public EnrolmentStatus? Status { get; set; }
}

public record EnrolmentResponse(
    int EnrolmentId, int UserId, string ParticipantName, int EventId, string EventName,
    DateTime EventDate, int CategoryId, string CategoryName, DateTime EnrolledAt, EnrolmentStatus Status);

// ---------------------------------------------------------------- Results
public class ResultRequest
{
    [Range(1, int.MaxValue)] public int EnrolmentId { get; set; }
    /// <summary>Finish time as hh:mm:ss, e.g. 01:52:30.</summary>
    [Required, RegularExpression(@"^\d{1,2}:\d{2}:\d{2}$", ErrorMessage = "FinishTime must be in the format hh:mm:ss.")]
    public string FinishTime { get; set; } = "";
    /// <summary>Overall finishing position within the event (1 = winner).</summary>
    [Range(1, 100000)] public int FinishPosition { get; set; }
}

public record ResultResponse(
    int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate,
    string ParticipantName, string CategoryName, string FinishTime, int FinishPosition, int TotalFinishers);
