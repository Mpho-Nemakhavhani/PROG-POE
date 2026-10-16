using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[Route("api/events")]
public class EventsController(AppDbContext db) : ApiControllerBase
{
    /// <summary>List upcoming events, with optional filters.</summary>
    /// <param name="province">Only events in this province (e.g. Gauteng).</param>
    /// <param name="eventType">Run, Walk or Cycle.</param>
    /// <param name="dateFrom">Only events on or after this date.</param>
    /// <param name="dateTo">Only events on or before this date.</param>
    /// <response code="200">Array of upcoming events ordered by date.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetUpcoming(
        [FromQuery] string? province,
        [FromQuery] string? eventType,
        [FromQuery(Name = "dateFrom")] DateTime? dateFrom,
        [FromQuery(Name = "dateTo")] DateTime? dateTo)
    {
        var query = db.Events.AsNoTracking().Where(e => e.Status == EventStatuses.Upcoming);

        if (!string.IsNullOrWhiteSpace(province))
        {
            var p = province.Trim().ToLower();
            query = query.Where(e => e.Province.ToLower() == p);
        }
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var t = eventType.Trim().ToLower();
            query = query.Where(e => e.EventType.ToLower() == t);
        }
        if (dateFrom.HasValue) query = query.Where(e => e.EventDate >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(e => e.EventDate <= dateTo.Value);

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(EventResponse.From));
    }

    /// <summary>Get full details for one event, including its categories.</summary>
    /// <response code="200">The event with nested categories.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailResponse>> GetById(int id)
    {
        var ev = await db.Events.AsNoTracking().Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventId == id);
        return ev is null ? NotFound() : EventDetailResponse.FromDetail(ev);
    }

    /// <summary>Create a new event (Organisers only). The logged-in Organiser becomes the owner.</summary>
    /// <response code="201">The new event.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> Create(CreateEventRequest request)
    {
        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            Name = request.Name.Trim(),
            Description = request.Description,
            EventType = request.EventType,
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            Province = request.Province.Trim(),
            Status = EventStatuses.Upcoming
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, EventResponse.From(ev));
    }

    /// <summary>Update an event (only the owning Organiser).</summary>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or does not own this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Update(int id, UpdateEventRequest request)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only edit events you own.");

        ev.Name = request.Name.Trim();
        ev.Description = request.Description;
        ev.EventType = request.EventType;
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.Province = request.Province.Trim();
        ev.Status = request.Status;
        await db.SaveChangesAsync();
        return EventResponse.From(ev);
    }

    /// <summary>Delete an event and its categories, route info, enrolments and results (only the owning Organiser).</summary>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or does not own this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await db.Events.Include(e => e.Categories).Include(e => e.Route).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only delete events you own.");

        // Enrolments reference categories with no cascade (as in the Part 1 SQL), so remove them (and their results) first.
        var enrolments = await db.Enrolments.Include(e => e.Result).Where(e => e.Category.EventId == id).ToListAsync();
        db.Results.RemoveRange(enrolments.Where(e => e.Result is not null).Select(e => e.Result!));
        db.Enrolments.RemoveRange(enrolments);
        db.Events.Remove(ev); // categories and route info are removed by cascade
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Get route and preparation info (map, elevation, weather notes) for an event.</summary>
    /// <response code="200">The route info.</response>
    /// <response code="404">The event does not exist or has no route info captured.</response>
    [HttpGet("{id:int}/route")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RouteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteResponse>> GetRoute(int id)
    {
        var route = await db.RouteInfos.AsNoTracking().FirstOrDefaultAsync(r => r.EventId == id);
        return route is null ? NotFound() : RouteResponse.From(route);
    }

    /// <summary>Create or replace the route info for an event (only the owning Organiser).</summary>
    /// <remarks>Small addition to the Part 1 plan so that route info can actually be captured through the API.</remarks>
    /// <response code="200">The saved route info.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or does not own this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPut("{id:int}/route")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(RouteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteResponse>> UpsertRoute(int id, RouteRequest request)
    {
        var ev = await db.Events.Include(e => e.Route).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only edit events you own.");

        var route = ev.Route ?? new RouteInfo { EventId = id };
        route.RouteMapUrl = request.RouteMapUrl;
        route.ElevationGainM = request.ElevationGainM;
        route.StartLatitude = request.StartLatitude;
        route.StartLongitude = request.StartLongitude;
        route.WeatherNotes = request.WeatherNotes;
        if (ev.Route is null) db.RouteInfos.Add(route);

        await db.SaveChangesAsync();
        return RouteResponse.From(route);
    }
}
