using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

/// <summary>
/// Starts the real API in memory, but swaps SQL Server for an EF Core in-memory database
/// so the tests run anywhere (locally and on GitHub Actions) without a database server.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string OrganiserCode = "TEST-ORGANISER-CODE";
    private readonly string _dbName = "RaceDayTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registration:OrganiserCode"] = OrganiserCode
            }));
        builder.ConfigureServices(services =>
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName)));
    }
}

public static class TestHelpers
{
    public const string Password = "Passw0rd!23";

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@test.com";

    public static RegisterRequest RegisterBody(string role, string? email = null, string? organiserCode = null) => new()
    {
        FullName = "Test User",
        Email = email ?? UniqueEmail(),
        Password = Password,
        Role = role,
        OrganiserCode = organiserCode ?? (role == "Organiser" ? ApiFactory.OrganiserCode : null)
    };

    /// <summary>Registers a brand-new user and returns a client that is already signed in as them.</summary>
    public static async Task<(HttpClient Client, UserResponse User, string Email)> RegisterAsync(ApiFactory factory, string role)
    {
        var client = factory.CreateClient();
        var body = RegisterBody(role);
        var response = await client.PostAsJsonAsync("/api/auth/register", body);
        response.EnsureSuccessStatusCode();
        var user = (await response.Content.ReadFromJsonAsync<UserResponse>())!;
        return (client, user, body.Email);
    }

    public static CreateEventRequest NewEvent(string name = "Test Run") => new()
    {
        Name = name,
        Description = "A test event",
        EventType = "Run",
        EventDate = DateTime.UtcNow.AddDays(30),
        Location = "Benoni Lake",
        Province = "Gauteng"
    };

    public static UpdateEventRequest UpdateBody(string name, string status = "Upcoming") => new()
    {
        Name = name,
        Description = "Updated",
        EventType = "Walk",
        EventDate = DateTime.UtcNow.AddDays(40),
        Location = "Sea Point",
        Province = "Western Cape",
        Status = status
    };

    public static async Task<EventResponse> CreateEventAsync(HttpClient organiser, string name = "Test Run")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", NewEvent(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    public static async Task<CategoryResponse> CreateCategoryAsync(HttpClient organiser, int eventId, int maxParticipants = 100)
    {
        var body = new CategoryRequest { Name = "10km", DistanceKm = 10, EntryFee = 150, MaxParticipants = maxParticipants, MinAge = 12 };
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    public static async Task<EnrolmentResponse> EnrolAsync(HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsJsonAsync("/api/enrolments", new EnrolmentRequest { CategoryId = categoryId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentResponse>())!;
    }
}
