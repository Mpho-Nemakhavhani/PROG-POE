using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class EventTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // ---------- Organiser can manage events ----------

    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        var (organiser, user, _) = await TestHelpers.RegisterAsync(factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", TestHelpers.NewEvent("Benoni 10K"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal("Benoni 10K", created!.Name);
        Assert.Equal(user.UserId, created.OrganiserId);
        Assert.Equal("Upcoming", created.Status);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", TestHelpers.UpdateBody("Renamed Event", "Cancelled"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal("Renamed Event", updated!.Name);
        Assert.Equal("Cancelled", updated.Status);
        Assert.Equal("Walk", updated.EventType);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser);

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_RemovesItsCategoriesAndEnrolments()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);
        await TestHelpers.EnrolAsync(participant, category.CategoryId);

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var mine = await participant.GetFromJsonAsync<List<EnrolmentResponse>>("/api/enrolments/me");
        Assert.Empty(mine!);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidType_IsRejected()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var body = TestHelpers.NewEvent();
        body.EventType = "Swim";

        var response = await organiser.PostAsJsonAsync("/api/events", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithMissingName_IsRejected()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var body = TestHelpers.NewEvent();
        body.Name = "";

        var response = await organiser.PostAsJsonAsync("/api/events", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_ThatDoesNotExist_ReturnsNotFound()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var response = await organiser.PutAsJsonAsync("/api/events/999999", TestHelpers.UpdateBody("x"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Role-based access: wrong role / not logged in ----------

    [Fact]
    public async Task Participant_CannotCreateEvent()
    {
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var response = await participant.PostAsJsonAsync("/api/events", TestHelpers.NewEvent());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteEvent()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);

        var put = await participant.PutAsJsonAsync($"/api/events/{ev.EventId}", TestHelpers.UpdateBody("Hacked"));
        var delete = await participant.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUpdateOrDelete_AnotherOrganisersEvent()
    {
        var (owner, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (other, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(owner);

        var put = await other.PutAsJsonAsync($"/api/events/{ev.EventId}", TestHelpers.UpdateBody("Stolen"));
        var delete = await other.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCreateUpdateOrDeleteEvent()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/events", TestHelpers.NewEvent())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync($"/api/events/{ev.EventId}", TestHelpers.UpdateBody("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    // ---------- Both roles / anyone can view events ----------

    [Fact]
    public async Task Anyone_CanListAndViewEvents()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser, "Public Visible Event");
        await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);
        var anonymous = factory.CreateClient();

        foreach (var client in new[] { anonymous, participant, organiser })
        {
            var list = await client.GetFromJsonAsync<List<EventResponse>>("/api/events");
            Assert.Contains(list!, e => e.EventId == ev.EventId);

            var detail = await client.GetFromJsonAsync<EventDetailResponse>($"/api/events/{ev.EventId}");
            Assert.Single(detail!.Categories);
        }
    }

    [Fact]
    public async Task ListEvents_CanFilterByProvinceAndType()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var gauteng = await TestHelpers.CreateEventAsync(organiser, "Gauteng Run");

        var other = TestHelpers.NewEvent("Cape Cycle");
        other.Province = "Western Cape";
        other.EventType = "Cycle";
        var capeResponse = await organiser.PostAsJsonAsync("/api/events", other);
        var cape = await capeResponse.Content.ReadFromJsonAsync<EventResponse>();

        var byProvince = await organiser.GetFromJsonAsync<List<EventResponse>>("/api/events?province=Western%20Cape");
        Assert.Contains(byProvince!, e => e.EventId == cape!.EventId);
        Assert.DoesNotContain(byProvince!, e => e.EventId == gauteng.EventId);

        var byType = await organiser.GetFromJsonAsync<List<EventResponse>>("/api/events?eventType=Run");
        Assert.Contains(byType!, e => e.EventId == gauteng.EventId);
        Assert.DoesNotContain(byType!, e => e.EventId == cape!.EventId);
    }

    [Fact]
    public async Task GetEvent_ThatDoesNotExist_ReturnsNotFound()
    {
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/events/999999")).StatusCode);
    }

    // ---------- Categories ----------

    [Fact]
    public async Task Organiser_CanManageCategories_OfOwnEvent()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var category = await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);

        var update = await organiser.PutAsJsonAsync($"/api/categories/{category.CategoryId}",
            new CategoryRequest { Name = "21km", DistanceKm = 21.1m, EntryFee = 200, MaxParticipants = 50, MinAge = 16 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("21km", (await update.Content.ReadFromJsonAsync<CategoryResponse>())!.Name);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/categories/{category.CategoryId}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotAddCategory()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser);

        var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CategoryRequest { Name = "5km", DistanceKm = 5, EntryFee = 50, MaxParticipants = 10, MinAge = 5 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotAddCategory_ToAnotherOrganisersEvent()
    {
        var (owner, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (other, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(owner);

        var response = await other.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CategoryRequest { Name = "5km", DistanceKm = 5, EntryFee = 50, MaxParticipants = 10, MinAge = 5 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anyone_CanViewCategories()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        await TestHelpers.CreateCategoryAsync(organiser, ev.EventId);

        var categories = await factory.CreateClient().GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.EventId}/categories");
        Assert.Single(categories!);
    }

    [Fact]
    public async Task CreateCategory_WithInvalidValues_IsRejected()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CategoryRequest { Name = "Bad", DistanceKm = -5, EntryFee = 10, MaxParticipants = 0, MinAge = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Route info ----------

    [Fact]
    public async Task RouteInfo_CanBeSavedByOwner_AndReadByAnyone()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser);
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/events/{ev.EventId}/route")).StatusCode);

        var save = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}/route",
            new RouteRequest { RouteMapUrl = "https://maps.example.com/r1", ElevationGainM = 45, WeatherNotes = "Mild" });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var route = await anonymous.GetFromJsonAsync<RouteResponse>($"/api/events/{ev.EventId}/route");
        Assert.Equal(45, route!.ElevationGainM);
    }
}
