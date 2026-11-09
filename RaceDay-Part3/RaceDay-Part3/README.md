# RaceDay – Part 3 (MVC Web Application)

This repository now contains the Part 1 database design, Part 2 REST API and Part 3 ASP.NET Core MVC front end.

## Solution structure
- `Part2-API/` – ASP.NET Core 8 REST API, EF Core/SQL Server, session authentication, Azure Blob upload endpoint and xUnit tests.
- `Part3-MVC/` – ASP.NET Core 8 MVC web application. It communicates with the API through `RaceDayApiClient`; it contains no EF Core or direct database access.
- `docs/` – database schema, ERD and API endpoint plan.
- `docker-compose.yml` – local SQL Server + Azurite Blob Storage + API + MVC stack.

## Part 3 functionality
- Registration and login for Organiser and Participant roles.
- The MVC application stores the API session cookie server-side and uses an MVC authentication cookie for protected page access.
- Public upcoming-event listing, event details, category listing and public leaderboard.
- Organiser dashboard: create, edit and delete own events; create/delete categories; review event enrolments; capture results.
- Participant dashboard: enrol in one category per event, select payment method, cancel enrolments, view own results and update profile.
- Event banner and Participant profile-picture uploads are sent from MVC to the API. The API validates the files and stores them in separate Azure Blob Storage containers; the MVC app never talks to Blob Storage directly.
- Bootstrap responsive layout, consistent RaceDay styling, role-aware navigation, validation messages and enrolment status colours.

## Run locally (Visual Studio / .NET 8)
1. Ensure SQL Server is running and update `Part2-API/src/RaceDay.Api/appsettings.json` with your SQL Server connection string.
2. Configure Blob Storage in API configuration:
   ```json
   "AzureBlobStorage": {
     "ConnectionString": "<your Azure Storage connection string>",
     "Container": "event-banners",
     "ProfileContainer": "profile-pictures"
   }
   ```
   The storage account/container is created by the API when the first banner is uploaded.
3. Start the API:
   ```bash
   dotnet run --project Part2-API/src/RaceDay.Api
   ```
   API / Swagger: `http://localhost:5080/swagger`
4. In a second terminal, start MVC:
   ```bash
   dotnet run --project Part3-MVC
   ```
   Open the MVC URL printed by `dotnet run` (normally `http://localhost:5xxx`).
5. Register an Organiser account and create an event. Register a separate Participant account to enrol and view results.

The API and MVC must share the API base URL configured at `Part3-MVC/appsettings.json` (`Api:BaseUrl`). In production, use HTTPS and a persistent distributed session store rather than in-memory session.

## Run with Docker (single command)
Docker Desktop must be running:
```bash
docker compose up --build
```
- MVC: `http://localhost:8080`
- API Swagger: `http://localhost:5080/swagger`
- SQL Server: `localhost,1433`
- Azurite Blob endpoint: `http://localhost:10000`

The compose file uses SQL Server and Azurite for local development. For a real Azure deployment, replace the Azurite connection string with an Azure Storage connection string held in secrets/environment configuration. Do not commit production credentials.

## Build and tests
```bash
dotnet restore Part2-API/RaceDay.sln
dotnet build Part2-API/RaceDay.sln --configuration Release
dotnet test Part2-API/RaceDay.sln --configuration Release
```
GitHub Actions performs restore, Release build and API test execution on every push and pull request.

## Important implementation notes
- All MVC data operations are HTTP calls to Part 2 endpoints. No MVC controller references `RaceDayDbContext` or uses EF Core.
- The API's existing session authentication remains authoritative. MVC forwards the API session cookie with authenticated API requests; role claims in the MVC cookie protect MVC routes.
- The optional `BannerImageUrl` column is added safely at API startup for an existing SQL Server database. Existing events remain valid with a null banner URL.
- API event deletion and enrolment/result rules remain enforced by the API. The MVC interface displays API validation and conflict messages rather than duplicating business logic.
- The API's Blob Storage upload endpoints accept JPG, PNG and WebP images up to 5 MB. Event banners are restricted to the owning Organiser; profile pictures belong to the authenticated user.

## GitHub submission checklist
- Push the complete repository to your Part 3 branch.
- Keep at least 20 meaningful commits (the supplied ZIP is a source snapshot and cannot create those commits on your behalf).
- Confirm the GitHub Actions workflow is green.
- Record an unlisted YouTube demonstration covering both roles, MVC/API communication, Blob Storage and Docker.
- Add the repository URL and YouTube URL to your submission.
