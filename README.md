# RaceDay - Part 2: RESTful API

ASP.NET Core 8 Web API for the RaceDay event management system, backed by SQL Server through Entity Framework Core (Code-First), with cookie-session authentication, role-based access (Organiser / Participant), Swagger documentation, xUnit tests and a GitHub Actions CI pipeline.

## Links

- **YouTube demo (unlisted):** `<paste link here>`
- **CI status:** see screenshot below

## CI/CD

Every push runs `.github/workflows/part2-ci.yml`, which restores, builds and runs all unit tests.

![Green build](docs/ci-green-build.png)

## Tech stack

- ASP.NET Core 8 Web API (controllers)
- Entity Framework Core 8, SQL Server, Code-First
- Cookie-based session authentication, PBKDF2 password hashing
- Swashbuckle (Swagger UI)
- xUnit + `WebApplicationFactory` (EF Core in-memory database for tests)

## Setup and run

### Prerequisites
- .NET 8 SDK
- SQL Server or SQL Server LocalDB (installed with Visual Studio's "ASP.NET and web development" workload)

### 1. Configure the database
Edit `RaceDay.Api/appsettings.json` if your SQL Server is not LocalDB:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=RaceDayDB;Trusted_Connection=True;TrustServerCertificate=True"
}
```

### 2. Create the database (Code-First migrations)
From the `Part2-API` folder:

```bash
dotnet tool install --global dotnet-ef      # once
dotnet ef migrations add InitialCreate --project RaceDay.Api
dotnet ef database update --project RaceDay.Api
```

The API also applies migrations (or creates the schema if there are none) on startup. The schema matches the Part 1 ERD and SQL script.

### 3. Run
```bash
dotnet run --project RaceDay.Api
```
or press **F5** in Visual Studio. Swagger UI opens at `https://localhost:7100/swagger`.

### 4. Try it in Swagger
1. `POST /api/auth/register` - register a Participant, or an Organiser (needs the invite code below). You are signed in automatically.
2. Or `POST /api/auth/login`. Swagger UI (running in your browser) keeps the session cookie and sends it on every later request.
3. Call any endpoint. Use `POST /api/auth/logout` to switch user.

**Organiser invite code (development):** `RACEDAY-ORGANISER-2026` (set in `appsettings.json` under `Registration:OrganiserCode`).

### 5. Run the tests
```bash
dotnet test
```
Tests use an in-memory database, so no SQL Server is needed.

## The two roles

| | Organiser | Participant |
|---|---|---|
| Registration | Needs the organiser invite code | Open to anyone |
| Events | Create, update and delete **their own** events; view all | View all |
| Categories | Add, update, delete for **their own** events; view all | View all |
| Enrolments | View all enrolments for their events; cancel enrolments in their events | Enter an event by choosing a category; view and cancel their own |
| Results | Capture and correct results for their events | View their own results |
| Profile | View/update own; view any user by id | View/update own |

## Authentication design

- **Registration** collects name, email, password, phone (optional) and role. Because anyone could otherwise pick "Organiser", that role requires an invite code that RaceDay gives to genuine organisers.
- **Passwords** are never stored. They are hashed with PBKDF2-HMAC-SHA256 (100,000 iterations, random 16-byte salt per user) and stored in `Users.PasswordHash`.
- **Sessions:** on login the API issues a signed, HttpOnly cookie (`RaceDay.Session`) holding the user's id and role. It is validated on every request (8 hours, sliding).
- **Authorisation:** `[Authorize]` protects non-public endpoints (401 if not logged in); `[Authorize(Roles = ...)]` enforces role (403 for the wrong role). Ownership is checked in the controllers, so an Organiser cannot touch another Organiser's events, categories, enrolments or results.

## Code structure

```
Part2-API/
  RaceDay.Api/
    Controllers/   Auth, Users, Events, Categories, Enrolments, Results (+ shared base class)
    Data/          AppDbContext - EF Core mapping that mirrors the Part 1 SQL script
    Models/        Entities: User, Event, Category, RouteInfo, Enrolment, Result
    Dtos/          Request/response objects with validation attributes
    Services/      PasswordHasher, claims helper
    Program.cs     DI, cookie auth, Swagger, startup
  RaceDay.Api.Tests/
    AuthTests, EventTests, EnrolmentTests, ResultTests, TestSupport
```

## Endpoints

All endpoints are documented in Swagger (purpose, request body, responses).

| Method | Route | Access |
|---|---|---|
| POST | `/api/auth/register` | Public |
| POST | `/api/auth/login` | Public |
| POST | `/api/auth/logout` | Logged in |
| GET | `/api/users/me` | Logged in |
| PUT | `/api/users/me` | Logged in |
| GET | `/api/users/{id}` | Organiser |
| GET | `/api/events` (filters: `province`, `eventType`, `dateFrom`, `dateTo`) | Public |
| GET | `/api/events/{id}` | Public |
| POST | `/api/events` | Organiser |
| PUT | `/api/events/{id}` | Organiser (owner) |
| DELETE | `/api/events/{id}` | Organiser (owner) |
| GET | `/api/events/{id}/route` | Public |
| PUT | `/api/events/{id}/route` | Organiser (owner) |
| GET | `/api/events/{eventId}/categories` | Public |
| POST | `/api/events/{eventId}/categories` | Organiser (owner) |
| PUT | `/api/categories/{id}` | Organiser (owner) |
| DELETE | `/api/categories/{id}` | Organiser (owner) |
| POST | `/api/enrolments` | Participant |
| GET | `/api/enrolments/me` | Participant |
| GET | `/api/events/{eventId}/enrolments` | Organiser (owner) |
| DELETE | `/api/enrolments/{id}` | Participant (own) or Organiser (owner) |
| POST | `/api/results` | Organiser (owner) |
| PUT | `/api/results/{id}` | Organiser (owner) |
| GET | `/api/results/me` | Participant |
| GET | `/api/events/{eventId}/results` | Public |

### Differences from the Part 1 plan
- **Sessions instead of JWT.** The Part 2 brief asks for session management, so login issues a session cookie rather than returning a JWT.
- Added `POST /api/auth/logout` (needed to end a session) and `PUT /api/events/{id}/route` (so route info can be created through the API).
- Deleting a category that already has enrolments returns `409 Conflict`.
- Cancelling an enrolment sets its status to `Cancelled` (kept for history); the participant can re-enrol later.

## Unit tests

Tests start the real API in memory with `WebApplicationFactory` and cover success and failure paths for:
- Registration and login (valid, duplicate email, bad role, bad organiser code, wrong password, hashed password, sessions, logout)
- Event and category management by Organisers (create, update, delete, ownership)
- Role enforcement: Participants rejected from Organiser actions (403), Organisers rejected from Participant actions (403), anonymous requests rejected (401)
- Event enrolments (enrol, race number generated, enrolment recorded against participant/event/category, duplicate, full category, unknown category, organiser view, cancellation)
- Results (capture, correct, participant views own, leaderboard ordering, validation)
