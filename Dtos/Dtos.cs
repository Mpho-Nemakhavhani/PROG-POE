using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

// ---------- Authentication & users ----------

public class RegisterRequest
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required, RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Role must be 'Organiser' or 'Participant'.")]
    public string Role { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Invite code issued by RaceDay. Required only when registering as an Organiser.</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}

public class UserResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }

    public static UserResponse From(User u) => new()
    {
        UserId = u.UserId,
        FullName = u.FullName,
        Email = u.Email,
        Role = u.Role,
        PhoneNumber = u.PhoneNumber,
        CreatedAt = u.CreatedAt
    };
}

public class LoginResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

// ---------- Events ----------

public class CreateEventRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [Required, RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be 'Run', 'Walk' or 'Cycle'.")]
    public string EventType { get; set; } = string.Empty;

    [Required]
    public DateTime? EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Province { get; set; } = string.Empty;
}

public class UpdateEventRequest : CreateEventRequest
{
    /// <summary>Upcoming, Completed or Cancelled.</summary>
    [Required, RegularExpression("^(Upcoming|Completed|Cancelled)$", ErrorMessage = "Status must be 'Upcoming', 'Completed' or 'Cancelled'.")]
    public string Status { get; set; } = string.Empty;
}

public class EventResponse
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public static EventResponse From(Event e) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        Name = e.Name,
        Description = e.Description,
        EventType = e.EventType,
        EventDate = e.EventDate,
        Location = e.Location,
        Province = e.Province,
        Status = e.Status,
        CreatedAt = e.CreatedAt
    };
}

public class EventDetailResponse : EventResponse
{
    public List<CategoryResponse> Categories { get; set; } = [];

    public static EventDetailResponse FromDetail(Event e) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        Name = e.Name,
        Description = e.Description,
        EventType = e.EventType,
        EventDate = e.EventDate,
        Location = e.Location,
        Province = e.Province,
        Status = e.Status,
        CreatedAt = e.CreatedAt,
        Categories = e.Categories.OrderBy(c => c.DistanceKm).Select(CategoryResponse.From).ToList()
    };
}

// ---------- Route info ----------

public class RouteRequest
{
    [StringLength(300)]
    public string? RouteMapUrl { get; set; }

    [Range(0, 10000)]
    public int? ElevationGainM { get; set; }

    [Range(-90, 90)]
    public decimal? StartLatitude { get; set; }

    [Range(-180, 180)]
    public decimal? StartLongitude { get; set; }

    [StringLength(500)]
    public string? WeatherNotes { get; set; }
}

public class RouteResponse
{
    public int RouteId { get; set; }
    public int EventId { get; set; }
    public string? RouteMapUrl { get; set; }
    public int? ElevationGainM { get; set; }
    public decimal? StartLatitude { get; set; }
    public decimal? StartLongitude { get; set; }
    public string? WeatherNotes { get; set; }

    public static RouteResponse From(RouteInfo r) => new()
    {
        RouteId = r.RouteId,
        EventId = r.EventId,
        RouteMapUrl = r.RouteMapUrl,
        ElevationGainM = r.ElevationGainM,
        StartLatitude = r.StartLatitude,
        StartLongitude = r.StartLongitude,
        WeatherNotes = r.WeatherNotes
    };
}

// ---------- Categories ----------

public class CategoryRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 9999.99)]
    public decimal DistanceKm { get; set; }

    [Range(0, 999999.99)]
    public decimal EntryFee { get; set; }

    [Range(1, 1000000)]
    public int MaxParticipants { get; set; }

    [Range(0, 120)]
    public int MinAge { get; set; }
}

public class CategoryResponse
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }
    public int MinAge { get; set; }

    public static CategoryResponse From(Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        Name = c.Name,
        DistanceKm = c.DistanceKm,
        EntryFee = c.EntryFee,
        MaxParticipants = c.MaxParticipants,
        MinAge = c.MinAge
    };
}

// ---------- Enrolments ----------

public class EnrolmentRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "CategoryId is required.")]
    public int CategoryId { get; set; }
}

public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RaceNumber { get; set; }

    /// <summary>Only populated when an Organiser views the enrolments for their event.</summary>
    public string? ParticipantName { get; set; }
    public string? ParticipantEmail { get; set; }

    /// <summary>Requires Category and Category.Event to be loaded (and Participant when includeParticipant is true).</summary>
    public static EnrolmentResponse From(Enrolment e, bool includeParticipant = false) => new()
    {
        EnrolmentId = e.EnrolmentId,
        ParticipantId = e.ParticipantId,
        CategoryId = e.CategoryId,
        CategoryName = e.Category.Name,
        EventId = e.Category.EventId,
        EventName = e.Category.Event.Name,
        EventDate = e.Category.Event.EventDate,
        EnrolmentDate = e.EnrolmentDate,
        Status = e.Status,
        RaceNumber = e.RaceNumber,
        ParticipantName = includeParticipant ? e.Participant.FullName : null,
        ParticipantEmail = includeParticipant ? e.Participant.Email : null
    };
}

// ---------- Results ----------

public class CreateResultRequest : UpdateResultRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "EnrolmentId is required.")]
    public int EnrolmentId { get; set; }
}

public class UpdateResultRequest
{
    /// <summary>Finish time as hh:mm:ss. Required when Status is Finished.</summary>
    public TimeSpan? FinishTime { get; set; }

    /// <summary>Finishing position (1 = first). Required when Status is Finished.</summary>
    [Range(1, 1000000)]
    public int? Position { get; set; }

    /// <summary>Finished, DNF or DSQ.</summary>
    [Required, RegularExpression("^(Finished|DNF|DSQ)$", ErrorMessage = "Status must be 'Finished', 'DNF' or 'DSQ'.")]
    public string Status { get; set; } = ResultStatuses.Finished;
}

public class ResultResponse
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string ParticipantName { get; set; } = string.Empty;
    public string? RaceNumber { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }

    /// <summary>Requires Enrolment, Enrolment.Participant and Enrolment.Category.Event to be loaded.</summary>
    public static ResultResponse From(Result r) => new()
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        EventId = r.Enrolment.Category.EventId,
        EventName = r.Enrolment.Category.Event.Name,
        CategoryName = r.Enrolment.Category.Name,
        ParticipantName = r.Enrolment.Participant.FullName,
        RaceNumber = r.Enrolment.RaceNumber,
        FinishTime = r.FinishTime,
        Position = r.Position,
        Status = r.Status,
        CapturedAt = r.CapturedAt
    };
}
