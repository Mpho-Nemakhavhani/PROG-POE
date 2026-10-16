using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class EnrolmentsController(AppDbContext db) : ApiControllerBase
{
    /// <summary>Enter the logged-in Participant into an event by choosing a category (Participants only).</summary>
    /// <remarks>
    /// The link between Participant, event (via the category) and category is recorded and a race number is generated.
    /// A previously cancelled enrolment for the same category is re-activated.
    /// </remarks>
    /// <response code="201">The enrolment, including the generated race number.</response>
    /// <response code="400">Category does not exist, the event is not open for entry, or the category is full.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not a Participant.</response>
    /// <response code="409">Already enrolled in this category.</response>
    [HttpPost("api/enrolments")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enrol(EnrolmentRequest request)
    {
        var userId = CurrentUserId;
        var category = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category is null) return BadRequestProblem("That category does not exist.");
        if (category.Event.Status != EventStatuses.Upcoming) return BadRequestProblem("This event is not open for entries.");

        var existing = await db.Enrolments.FirstOrDefaultAsync(e => e.ParticipantId == userId && e.CategoryId == category.CategoryId);
        if (existing is not null && existing.Status != EnrolmentStatuses.Cancelled)
            return ConflictProblem("You are already enrolled in this category.");

        var taken = await db.Enrolments.CountAsync(e => e.CategoryId == category.CategoryId && e.Status != EnrolmentStatuses.Cancelled);
        if (taken >= category.MaxParticipants) return BadRequestProblem("This category is full.");

        var enrolment = existing ?? new Enrolment { ParticipantId = userId, CategoryId = category.CategoryId };
        enrolment.Status = EnrolmentStatuses.Confirmed;
        enrolment.EnrolmentDate = DateTime.UtcNow;
        if (existing is null) db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();

        if (string.IsNullOrEmpty(enrolment.RaceNumber))
        {
            enrolment.RaceNumber = $"RD-{enrolment.EnrolmentId:D4}";
            await db.SaveChangesAsync();
        }

        enrolment.Category = category;
        return Created("/api/enrolments/me", EnrolmentResponse.From(enrolment));
    }

    /// <summary>List all enrolments (past and upcoming) of the logged-in Participant.</summary>
    /// <response code="200">Array of enrolments, newest event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("api/enrolments/me")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetMine()
    {
        var userId = CurrentUserId;
        var enrolments = await db.Enrolments.AsNoTracking()
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Where(e => e.ParticipantId == userId)
            .OrderByDescending(e => e.Category.Event.EventDate)
            .ToListAsync();
        return Ok(enrolments.Select(e => EnrolmentResponse.From(e)));
    }

    /// <summary>List all enrolments for an event, with participant details (only the owning Organiser).</summary>
    /// <response code="200">Array of enrolments with participant details.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or does not own the event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetForEvent(int eventId)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only view enrolments for events you own.");

        var enrolments = await db.Enrolments.AsNoTracking()
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Where(e => e.Category.EventId == eventId)
            .OrderBy(e => e.Category.Name).ThenBy(e => e.EnrolmentDate)
            .ToListAsync();
        return Ok(enrolments.Select(e => EnrolmentResponse.From(e, includeParticipant: true)));
    }

    /// <summary>Cancel an enrolment (the enrolled Participant, or the Organiser who owns the event).</summary>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is neither the enrolled Participant nor the event's Organiser.</response>
    /// <response code="404">Enrolment does not exist.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = CurrentUserId;
        var enrolment = await db.Enrolments.Include(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound();

        var isParticipant = enrolment.ParticipantId == userId;
        var isOwner = enrolment.Category.Event.OrganiserId == userId;
        if (!isParticipant && !isOwner) return ForbiddenProblem("Only the enrolled participant or the event's organiser can cancel an enrolment.");

        enrolment.Status = EnrolmentStatuses.Cancelled;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
