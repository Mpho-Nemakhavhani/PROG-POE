using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class ResultsController(AppDbContext db) : ApiControllerBase
{
    private IQueryable<Result> ResultsWithContext() =>
        db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event);

    /// <summary>Capture a result (finish time, position, status) for an enrolment (only the event's Organiser).</summary>
    /// <remarks>When Status is "Finished", both finishTime and position are required. DNF/DSQ may omit them.</remarks>
    /// <response code="201">The captured result.</response>
    /// <response code="400">Validation errors, enrolment not found or cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or does not own the event.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpPost("api/results")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> Capture(CreateResultRequest request)
    {
        ValidateFinished(request);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var enrolment = await db.Enrolments.Include(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == request.EnrolmentId);
        if (enrolment is null) return BadRequestProblem("That enrolment does not exist.");
        if (enrolment.Category.Event.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only capture results for events you own.");
        if (enrolment.Status == EnrolmentStatuses.Cancelled) return BadRequestProblem("Results cannot be captured for a cancelled enrolment.");
        if (await db.Results.AnyAsync(r => r.EnrolmentId == enrolment.EnrolmentId))
            return ConflictProblem("A result has already been captured for this enrolment. Use PUT /api/results/{id} to correct it.");

        var result = new Result
        {
            EnrolmentId = enrolment.EnrolmentId,
            CapturedByOrganiserId = CurrentUserId,
            FinishTime = request.FinishTime,
            Position = request.Position,
            Status = request.Status
        };
        db.Results.Add(result);
        await db.SaveChangesAsync();

        var saved = await ResultsWithContext().AsNoTracking().FirstAsync(r => r.ResultId == result.ResultId);
        return Created($"/api/events/{enrolment.Category.EventId}/results", ResultResponse.From(saved));
    }

    /// <summary>Correct a previously captured result (only the event's Organiser).</summary>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or does not own the event.</response>
    /// <response code="404">Result does not exist.</response>
    [HttpPut("api/results/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultResponse>> Update(int id, UpdateResultRequest request)
    {
        ValidateFinished(request);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var result = await ResultsWithContext().FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFound();
        if (result.Enrolment.Category.Event.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only edit results for events you own.");

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        result.Status = request.Status;
        await db.SaveChangesAsync();
        return ResultResponse.From(result);
    }

    /// <summary>Get the logged-in Participant's own result history across all events.</summary>
    /// <response code="200">Array of results with event and category context.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("api/results/me")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetMine()
    {
        var userId = CurrentUserId;
        var results = await ResultsWithContext().AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == userId)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .ToListAsync();
        return Ok(results.Select(ResultResponse.From));
    }

    /// <summary>Get the full results list (leaderboard) for an event.</summary>
    /// <response code="200">Array of results ordered by category, then position.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetForEvent(int eventId)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == eventId)) return NotFound();

        var results = await ResultsWithContext().AsNoTracking()
            .Where(r => r.Enrolment.Category.EventId == eventId)
            .OrderBy(r => r.Enrolment.Category.Name)
            .ThenBy(r => r.Position == null)
            .ThenBy(r => r.Position)
            .ToListAsync();
        return Ok(results.Select(ResultResponse.From));
    }

    private void ValidateFinished(UpdateResultRequest request)
    {
        if (request.Status == ResultStatuses.Finished)
        {
            if (request.FinishTime is null)
                ModelState.AddModelError(nameof(request.FinishTime), "FinishTime is required when Status is 'Finished'.");
            if (request.Position is null)
                ModelState.AddModelError(nameof(request.Position), "Position is required when Status is 'Finished'.");
        }
        if (request.FinishTime is { } t && t < TimeSpan.Zero)
            ModelState.AddModelError(nameof(request.FinishTime), "FinishTime cannot be negative.");
    }
}
