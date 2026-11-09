using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class ResultTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(HttpClient Organiser, HttpClient Participant, EventResponse Event, EnrolmentResponse Enrolment)> SetupAsync()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(participant, category.CategoryId);
        return (organiser, participant, ev, enrolment);
    }

    private static CreateResultRequest FinishedResult(int enrolmentId) => new()
    {
        EnrolmentId = enrolmentId,
        FinishTime = TimeSpan.FromMinutes(48) + TimeSpan.FromSeconds(32),
        Position = 1,
        Status = "Finished"
    };

    [Fact]
    public async Task Organiser_CanCaptureResult_AndParticipantCanSeeIt()
    {
        var (organiser, participant, ev, enrolment) = await SetupAsync();

        var capture = await organiser.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId));
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        var mine = await participant.GetFromJsonAsync<List<ResultResponse>>("/api/results/me");
        var result = Assert.Single(mine!);
        Assert.Equal(1, result.Position);
        Assert.Equal(TimeSpan.Parse("00:48:32"), result.FinishTime);
        Assert.Equal(ev.EventId, result.EventId);
    }

    [Fact]
    public async Task Organiser_CanCorrectResult()
    {
        var (organiser, _, _, enrolment) = await SetupAsync();
        var created = await (await organiser.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId)))
            .Content.ReadFromJsonAsync<ResultResponse>();

        var update = await organiser.PutAsJsonAsync($"/api/results/{created!.ResultId}",
            new UpdateResultRequest { FinishTime = TimeSpan.FromMinutes(50), Position = 2, Status = "Finished" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<ResultResponse>();
        Assert.Equal(2, updated!.Position);
        Assert.Equal(TimeSpan.FromMinutes(50), updated.FinishTime);
    }

    [Fact]
    public async Task Participant_CannotCaptureOrUpdateResults()
    {
        var (organiser, participant, _, enrolment) = await SetupAsync();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await participant.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId))).StatusCode);

        var created = await (await organiser.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId)))
            .Content.ReadFromJsonAsync<ResultResponse>();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await participant.PutAsJsonAsync($"/api/results/{created!.ResultId}",
                new UpdateResultRequest { FinishTime = TimeSpan.FromMinutes(1), Position = 1, Status = "Finished" })).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResult_ForAnotherOrganisersEvent()
    {
        var (_, _, _, enrolment) = await SetupAsync();
        var (other, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");

        var response = await other.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCaptureResult_OrViewMyResults()
    {
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/results", FinishedResult(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/results/me")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUse_ParticipantOnlyEndpoint_MyResults()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.GetAsync("/api/results/me")).StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_ForSameEnrolment_ReturnsConflict()
    {
        var (organiser, _, _, enrolment) = await SetupAsync();

        Assert.Equal(HttpStatusCode.Created, (await organiser.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await organiser.PostAsJsonAsync("/api/results", FinishedResult(enrolment.EnrolmentId))).StatusCode);
    }

    [Fact]
    public async Task CaptureResult_FinishedWithoutTimeOrPosition_IsRejected()
    {
        var (organiser, _, _, enrolment) = await SetupAsync();

        var response = await organiser.PostAsJsonAsync("/api/results",
            new CreateResultRequest { EnrolmentId = enrolment.EnrolmentId, Status = "Finished" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_DnfWithoutTime_IsAccepted()
    {
        var (organiser, _, _, enrolment) = await SetupAsync();

        var response = await organiser.PostAsJsonAsync("/api/results",
            new CreateResultRequest { EnrolmentId = enrolment.EnrolmentId, Status = "DNF" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForUnknownEnrolment_IsRejected()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var response = await organiser.PostAsJsonAsync("/api/results", FinishedResult(999999));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EventLeaderboard_IsPublic_AndOrderedByPosition()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (p1, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var (p2, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);
        var e1 = await TestHelpers.EnrolAsync(p1, category.CategoryId);
        var e2 = await TestHelpers.EnrolAsync(p2, category.CategoryId);

        // Capture the 2nd place first to prove ordering is done by the API.
        await organiser.PostAsJsonAsync("/api/results",
            new CreateResultRequest { EnrolmentId = e2.EnrolmentId, FinishTime = TimeSpan.FromMinutes(55), Position = 2, Status = "Finished" });
        await organiser.PostAsJsonAsync("/api/results",
            new CreateResultRequest { EnrolmentId = e1.EnrolmentId, FinishTime = TimeSpan.FromMinutes(45), Position = 1, Status = "Finished" });

        var board = await factory.CreateClient().GetFromJsonAsync<List<ResultResponse>>($"/api/events/{ev.EventId}/results");

        Assert.Equal(2, board!.Count);
        Assert.Equal(new[] { 1, 2 }, board.Select(r => r.Position!.Value).ToArray());
    }

    [Fact]
    public async Task EventLeaderboard_ForUnknownEvent_ReturnsNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/api/events/999999/results")).StatusCode);
    }
}
