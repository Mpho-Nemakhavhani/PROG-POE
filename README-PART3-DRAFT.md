# RaceDay – Part 3 (Automation of Web Application)

> **Status:** implementation draft. This repository combines the supplied Part 2 API source with a new .NET 8 MVC frontend. Do not submit until the verification and outstanding requirements below are completed.

## Solution structure

- `Part2-API/RaceDay.Api` – existing Part 2 REST API (EF Core/SQL Server).
- `Part2-API/RaceDay.Api.Tests` – supplied API tests.
- `Part3-MVC` – ASP.NET Core MVC frontend. MVC uses `HttpClient` to call API endpoints; it has no DbContext or direct database access.
- `docker-compose.yml` – local container orchestration draft.

## Requirements

- .NET 8 SDK
- Visual Studio 2022 (or .NET CLI)
- SQL Server LocalDB for local API development, or Docker Desktop for containers
- Azure Storage Account / Blob container (required to finish image upload requirement)

## Run locally

1. Open `Part2-API/RaceDay.sln` in Visual Studio.
2. Set `RaceDay.Api` and `RaceDay.Mvc` as multiple startup projects (Start both).
3. Ensure the API uses its Part 2 LocalDB connection string and starts on `https://localhost:7100` (or update `Part3-MVC/appsettings.json` `Api:BaseUrl` to the actual API URL).
4. Run the solution. MVC is configured for the API base URL `https://localhost:7100/`.
5. Register a Participant. To register an Organiser, supply the invite code configured in the API's `Registration:OrganiserCode`.

## Docker

Set environment variables before running:

```powershell
$env:MSSQL_SA_PASSWORD = "Use-A-Strong-Password-Here!"
$env:RACEDAY_ORGANISER_CODE = "Set-A-Private-Invite-Code"
docker compose up --build
```

Open:
- MVC: http://localhost:8080
- API Swagger: http://localhost:7100/swagger

Stop with `docker compose down`. Add `-v` only when you intentionally want to delete the SQL data volume.

## API integration and authentication

MVC calls the Part 2 API using `ApiClient`. The API session cookie is retained server-side in ASP.NET Core session and forwarded on subsequent API requests. MVC issues its own authentication cookie for role-aware authorization/navigation. MVC controllers call API endpoints only; do not add EF Core or database access to MVC.

## Azure Blob Storage – outstanding work

The assessment requires Blob Storage integration **inside the API**, including event banner and Participant profile picture upload endpoints, persistence of image URLs, validation and display in MVC. The supplied Part 2 API currently has no image URL properties or upload endpoints. The current MVC Profile page explicitly indicates profile image upload is not yet enabled. Do not claim this criterion is complete until:
1. Create an Azure Storage Account and private/public-read strategy appropriate to your lecturer's requirements.
2. Add `Azure.Storage.Blobs` to the API and configure connection settings via environment variables / user secrets (never commit storage keys).
3. Implement API-owned upload endpoints and persist the returned image URL for Event banners and User profile pictures (including EF migration).
4. Add MVC multipart upload forms that send files to those API endpoints, and display the returned images.
5. Verify upload, retrieval and access permissions using a real Azure container.

## Known implementation gaps to finish before submission

- Azure Blob Storage API integration, image persistence, and MVC banner/profile upload flows are not implemented in this draft.
- Organiser dashboard currently lists events returned by the public upcoming-events endpoint; the supplied API does not expose an organiser-owned all-events summary/enrolment aggregate endpoint. Extend the API to return accurate owner-scoped dashboard statistics.
- Results entry currently captures new results; editing an existing result and publishing workflow need to be tested/added as required.
- The Part 2 API has no category update/delete UI wired into MVC yet.
- Add server-side event filtering controls to the Participant event list.
- Review all MVC API error paths, antiforgery, validation, cookie forwarding and session expiry.
- Verify all API routes against the actual controller route attributes and test both roles end-to-end.
- Docker compose is a starting point. Confirm SQL Server healthcheck works with the Docker image version in use, test migrations and API-to-MVC connectivity.
- Update CI workflow and ensure the solution builds and tests successfully on GitHub Actions.
- Add your own meaningful Git commits (at least 20), GitHub repository link and unlisted YouTube demonstration link. These must reflect real work and cannot be fabricated.

## Assessment demonstration checklist

1. Register/login as Participant; browse upcoming events; view event categories; enrol; view enrolment status; view results/profile.
2. Register/login as Organiser with invite code; create/edit/delete event; create categories; inspect enrolments; capture and correct results.
3. Upload event banner and profile picture through MVC → API → Azure Blob Storage.
4. Show that MVC contains no database access.
5. Build and run with Docker Compose; demonstrate same functionality.
6. Show GitHub Actions passing for API tests and MVC build.
