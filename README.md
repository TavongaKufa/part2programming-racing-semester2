# RaceDay

RaceDay is a web-based event management system for South African road running, walking and cycling events. **Organisers** create events, categories and results; **Participants** browse events, enrol and track their race history.

This repository holds the Portfolio of Evidence:

| Part | Content | Location |
|---|---|---|
| 1 | ERD, API endpoint plan, SQL script | `/docs` |
| 2 | RESTful ASP.NET Core Web API + unit tests + CI | `/Part2-API` |
| 3 | MVC web app, Azure Blob Storage, Docker | added later |

## Video presentation

Part 2 walkthrough (unlisted YouTube): REPLACE_ME_YOUTUBE_LINK

## CI/CD – green build

![Green GitHub Actions build](docs/ci-green-build.png)

(Screenshot of the passing workflow in `.github/workflows/ci.yml`: REPLACE_ME until docs/ci-green-build.png is added.)

## The two roles

- **Organiser** – creates, edits and deletes their own events (name, description, date, location, distance, type Run/Walk/Cycle), manages age/distance categories, views all enrolments for their events, and captures finish times and positions after an event.
- **Participant** – creates an account, browses events and categories, enrols in an event by choosing a category, views their own enrolments (status Confirmed/Pending) and their personal results.

Role-based access is enforced on the server: no session → `401`, wrong role or not the owner → `403`.

## Database (Part 1 → Part 2 consistency)

- `docs/ERD.png` – 6 entities: Roles, Users, Events, Categories, Enrolments, Results.
- `docs/RaceDay.sql` – creates the `RaceDayDb` database, tables, constraints and seed data (2 Organisers, 2 Participants, 3 Events, 9 Categories, 5 Enrolments, 2 Results).
- `docs/API-Endpoint-Plan.md` – the 22 planned endpoints. The API implements exactly this plan.
- The EF Core Code First model (`Part2-API/RaceDay.Api/Data/AppDbContext.cs`) matches the ERD and SQL script. No deliberate differences. `ProfilePictureUrl` and `BannerImageUrl` are nullable columns reserved for Part 3.

## Setup and run (Part 2)

Requirements: .NET 8 SDK, SQL Server (LocalDB or SQL Server Express) and SSMS.

1. **Create the database** – open `docs/RaceDay.sql` in SSMS and execute it (creates `RaceDayDb` with sample data).
2. **Check the connection string** in `Part2-API/RaceDay.Api/appsettings.json` and change `Server=` to your SQL Server name (the one you use to log in to SSMS).
   *Alternative (Code First):* leave the script out and run `dotnet ef database update --project Part2-API/RaceDay.Api` to create an empty database containing the two roles.
3. **Run the API**
   ```
   cd Part2-API/RaceDay.Api
   dotnet run --launch-profile http
   ```
4. Open **http://localhost:5080/swagger**.
5. In Swagger, call `POST /api/auth/login` (e.g. `thandi.organiser@raceday.co.za` / `Password123!`). The session cookie is then sent automatically, so every other endpoint can be tried with *Try it out*. Other seeded users: `pieter.organiser@…`, `lerato.runner@…`, `sipho.cyclist@…` (all `@raceday.co.za`, same password).

## Run the unit tests

```
dotnet test Part2-API/RaceDay.sln
```

The tests start the real API in memory with an EF Core InMemory database (no SQL Server needed) and cover registration/login, session handling, role-based rejection (401/403), event management, categories, enrolments and results – both success and failure scenarios.

## Design decisions

- **Session-based authentication:** ASP.NET Core session stores `UserId` and `Role` at login; a `[SessionAuthorize]` filter checks it on every protected endpoint.
- **Passwords** are hashed with ASP.NET Core Identity's `PasswordHasher` (PBKDF2); plain text is never stored.
- **Role choice at registration:** the user selects Organiser or Participant when registering, as required by the brief.
- **Swagger:** every endpoint is documented with XML comments (purpose, expected data, response codes).
- **Errors** are returned as clean `ProblemDetails` JSON – no stack traces.

## AI disclosure

AI assistance (Claude by Anthropic) was used to help plan and draft the code, tests and documentation in this repository. I reviewed, ran and tested everything and explain it in my own words in the video. *(Edit this paragraph so it accurately reflects how you used AI.)*
