using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // ---------- Registration ----------

    [Fact]
    public async Task Register_Participant_Succeeds_And_DoesNotReturnPassword()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody("Participant"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"role\":\"Participant\"", raw);
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_Succeeds()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody("Organiser"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Organiser", user!.Role);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_IsRejected()
    {
        var client = factory.CreateClient();
        var body = TestHelpers.RegisterBody("Organiser");
        body.OrganiserCode = null;

        var response = await client.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongCode_IsRejected()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.RegisterBody("Organiser", organiserCode: "not-the-code"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail();
        var first = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody("Participant", email));
        var second = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody("Participant", email));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_IsRejected()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody("Admin"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_IsRejected()
    {
        var client = factory.CreateClient();
        var body = TestHelpers.RegisterBody("Participant");
        body.Password = "short";
        var response = await client.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_IsRejected()
    {
        var client = factory.CreateClient();
        var body = TestHelpers.RegisterBody("Participant", "not-an-email");
        var response = await client.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var (_, _, email) = await TestHelpers.RegisterAsync(factory, "Participant");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Users.SingleAsync(u => u.Email == email);

        Assert.NotEqual(TestHelpers.Password, stored.PasswordHash);
        Assert.DoesNotContain(TestHelpers.Password, stored.PasswordHash);
        Assert.StartsWith("v1.", stored.PasswordHash);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_WithCorrectCredentials_Succeeds_And_ReturnsRole()
    {
        var (_, _, email) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal("Organiser", body!.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var (_, _, email) = await TestHelpers.RegisterAsync(factory, "Participant");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "WrongPassword1" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = TestHelpers.UniqueEmail(), Password = TestHelpers.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Session behaviour ----------

    [Fact]
    public async Task ProtectedEndpoint_WithoutSession_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_AfterLogin_KeepsSession()
    {
        var (_, _, email) = await TestHelpers.RegisterAsync(factory, "Participant");
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = TestHelpers.Password });

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(email, me!.Email);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var (client, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    // ---------- Profile ----------

    [Fact]
    public async Task UpdateProfile_ChangesNameAndPhone()
    {
        var (client, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");

        var response = await client.PutAsJsonAsync("/api/users/me", new UpdateProfileRequest { FullName = "New Name", PhoneNumber = "0821234567" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await (await client.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("New Name", me!.FullName);
        Assert.Equal("0821234567", me.PhoneNumber);
    }

    [Fact]
    public async Task UpdateProfile_WithEmptyName_IsRejected()
    {
        var (client, _, _) = await TestHelpers.RegisterAsync(factory, "Participant");
        var response = await client.PutAsJsonAsync("/api/users/me", new UpdateProfileRequest { FullName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_AsOrganiser_Succeeds_AsParticipant_IsForbidden()
    {
        var (organiser, _, _) = await TestHelpers.RegisterAsync(factory, "Organiser");
        var (participant, participantUser, _) = await TestHelpers.RegisterAsync(factory, "Participant");

        Assert.Equal(HttpStatusCode.OK, (await organiser.GetAsync($"/api/users/{participantUser.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync($"/api/users/{participantUser.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync("/api/users/999999")).StatusCode);
    }
}
