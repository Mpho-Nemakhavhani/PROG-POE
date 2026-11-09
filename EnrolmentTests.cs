using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class EnrolmentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecorded()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, participantUser, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser, "Enrolment Event");
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);

        var response = await participant.PostAsJsonAsync("/api/enrolments", new EnrolmentRequest { CategoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await response.Content.ReadFromJsonAsync<EnrolmentResponse>();
        Assert.Equal(participantUser.UserId, enrolment!.ParticipantId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal("Confirmed", enrolment.Status);
        Assert.False(string.IsNullOrWhiteSpace(enrolment.RaceNumber));

        // The link between participant, event and category is stored and shows up in their list.
        var mine = await participant.GetFromJsonAsync<List<EnrolmentResponse>>("/api/enrolments/me");
        var recorded = Assert.Single(mine!);
        Assert.Equal(enrolment.EnrolmentId, recorded.EnrolmentId);
        Assert.Equal(ev.EventId, recorded.EventId);
        Assert.Equal(category.CategoryId, recorded.CategoryId);
    }

    [Fact]
    public async Task Enrolling_Twice_InSameCategory_ReturnsConflict()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);

        await TestHelpers.EnrolAsync(participant, category.CategoryId);
        var second = await participant.PostAsJsonAsync("/api/enrolments", new EnrolmentRequest { CategoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrolling_InUnknownCategory_IsRejected()
    {
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var response = await participant.PostAsJsonAsync("/api/enrolments", new EnrolmentRequest { CategoryId = 999999 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrolling_WithoutCategoryId_IsRejected()
    {
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var response = await participant.PostAsJsonAsync("/api/enrolments", new EnrolmentRequest { CategoryId = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrolling_InFullCategory_IsRejected()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (first, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var (second, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId, maxParticipants: 1);

        await TestHelpers.EnrolAsync(first, category.CategoryId);
        var response = await second.PostAsJsonAsync("/api/enrolments", new EnrolmentRequest { CategoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);

        var response = await organiser.PostAsJsonAsync("/api/enrolments", new EnrolmentRequest { CategoryId = category.CategoryId });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotEnrol_OrViewEnrolments()
    {
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/enrolments", new EnrolmentRequest { CategoryId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/enrolments/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/events/1/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUse_ParticipantOnlyEndpoint_MyEnrolments()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.GetAsync("/api/enrolments/me")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolments_ForOwnEvent_WithParticipantDetails()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, participantUser, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);
        await TestHelpers.EnrolAsync(participant, category.CategoryId);

        var response = await organiser.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<EnrolmentResponse>>();
        var enrolment = Assert.Single(list!);
        Assert.Equal(participantUser.UserId, enrolment.ParticipantId);
        Assert.Equal(participantUser.FullName, enrolment.ParticipantName);
        Assert.Equal(participantUser.Email, enrolment.ParticipantEmail);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolments_OfAnotherOrganisersEvent()
    {
        var (owner, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (other, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/events/{ev.EventId}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync($"/api/events/{ev.EventId}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_AndCanReEnrolAfterwards()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(participant, category.CategoryId);

        Assert.Equal(HttpStatusCode.NoContent, (await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}")).StatusCode);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentResponse>>("/api/enrolments/me");
        Assert.Equal("Cancelled", Assert.Single(mine!).Status);

        var again = await participant.PostAsJsonAsync("/api/enrolments", new EnrolmentRequest { CategoryId = category.CategoryId });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task OtherParticipant_CannotCancel_SomeoneElsesEnrolment()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (owner, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var (other, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(owner, category.CategoryId);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCancel_EnrolmentInOwnEvent()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(participant, category.CategoryId);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}")).StatusCode);
    }

    [Fact]
    public async Task CancelEnrolment_ThatDoesNotExist_ReturnsNotFound()
    {
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        Assert.Equal(HttpStatusCode.NotFound, (await participant.DeleteAsync("/api/enrolments/999999")).StatusCode);
    }
}
