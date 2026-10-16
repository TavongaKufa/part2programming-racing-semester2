# RaceDay – API Endpoint Plan

Planned before any API code was written. The Part 2 API implements exactly these endpoints.

**Conventions**

- All routes start with `/api/` and use plural nouns.
- **Authentication:** session based. `POST /api/auth/login` creates a server-side session (cookie) that stores the user id and role. Every endpoint except register and login requires that session.
- **Role required** values: `None (public)`, `Any (logged in)`, `Organiser`, `Participant`. "Organiser (owner)" means an Organiser who created the event in question.
- Missing/expired session → **401 Unauthorized**. Logged in with the wrong role (or not the owner) → **403 Forbidden**.
- Validation failures → **400 Bad Request** with a short description. Passwords are hashed and never returned.
- Enum values are sent as text: `eventType` = `Run` | `Walk` | `Cycle`; `categoryType` = `Age` | `Distance`; `status` = `Confirmed` | `Pending`.

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| POST | `/api/auth/register` | Creates a new account; the user chooses the Organiser or Participant role. Password is hashed before saving. | None (public) | `{ firstName, lastName, email, password, role, phoneNumber? }` | **201 Created** – profile. 400 Bad Request – invalid data/role. 409 Conflict – email already registered. |
| POST | `/api/auth/login` | Verifies credentials and starts a session that remembers the user and their role. | None (public) | `{ email, password }` | **200 OK** – profile incl. role. 401 Unauthorized – wrong email or password. |
| POST | `/api/auth/logout` | Ends the current session. | Any (logged in) | None | **200 OK** – confirmation message. 401 – not logged in. |
| GET | `/api/profile` | Returns the logged-in user's own profile. | Any (logged in) | None | **200 OK** – profile. 401 – not logged in. |
| PUT | `/api/profile` | Updates the logged-in user's own first name, last name and phone number. | Any (logged in) | `{ firstName, lastName, phoneNumber? }` | **200 OK** – updated profile. 400 – invalid data. 401 – not logged in. |
| GET | `/api/events` | Lists all events, soonest first. Optional query filters `type`, `upcomingOnly`, `search`. | Any (logged in) | None | **200 OK** – list of events. 401 – not logged in. |
| GET | `/api/events/mine` | Lists the events created by the logged-in Organiser, with enrolment counts (dashboard). | Organiser | None | **200 OK** – list of events. 401 – not logged in. 403 – not an Organiser. |
| GET | `/api/events/{id}` | Returns one event (name, description, date, location, distance, type). | Any (logged in) | None | **200 OK** – event. 401 – not logged in. 404 Not Found – no such event. |
| POST | `/api/events` | Creates an event owned by the logged-in Organiser. Date must be in the future. | Organiser | `{ name, description, eventDate, location, distanceKm, eventType }` | **201 Created** – event. 400 – invalid data / past date. 401. 403 – not an Organiser. |
| PUT | `/api/events/{id}` | Edits an event. | Organiser (owner) | `{ name, description, eventDate, location, distanceKm, eventType }` | **200 OK** – event. 400. 401. 403 – not owner. 404. |
| DELETE | `/api/events/{id}` | Deletes an event together with its categories, enrolments and results. | Organiser (owner) | None | **204 No Content**. 401. 403 – not owner. 404. |
| GET | `/api/events/{eventId}/categories` | Lists the age/distance categories of an event. | Any (logged in) | None | **200 OK** – list of categories. 401. 404 – no such event. |
| POST | `/api/events/{eventId}/categories` | Adds a category (e.g. "Under 20", "Senior", "10km") to an event. Names are unique per event. | Organiser (owner) | `{ name, categoryType }` | **201 Created** – category. 400. 401. 403 – not owner. 404. 409 – name already used. |
| PUT | `/api/categories/{id}` | Renames / changes a category. | Organiser (owner) | `{ name, categoryType }` | **200 OK** – category. 400. 401. 403. 404. 409 – name already used. |
| DELETE | `/api/categories/{id}` | Deletes a category that nobody has enrolled in yet. | Organiser (owner) | None | **204 No Content**. 401. 403. 404. 409 – category already has enrolments. |
| POST | `/api/events/{eventId}/enrolments` | Enrols the logged-in Participant in the event under a chosen category. Recorded immediately (status Confirmed). Category must belong to the event; event must be in the future; one enrolment per event. | Participant | `{ categoryId }` | **201 Created** – enrolment. 400 – wrong category / event already held. 401. 403 – not a Participant. 404 – no such event. 409 – already enrolled. |
| GET | `/api/enrolments/mine` | Lists every event the logged-in Participant has enrolled in, with category and status. | Participant | None | **200 OK** – list of enrolments. 401. 403 – not a Participant. |
| GET | `/api/events/{eventId}/enrolments` | Lists all enrolments (participant, category, status) for an event. | Organiser (owner) | None | **200 OK** – list of enrolments. 401. 403 – not owner. 404. |
| PUT | `/api/enrolments/{id}/status` | Sets an enrolment to Confirmed or Pending. | Organiser (owner) | `{ status }` | **200 OK** – enrolment. 400. 401. 403 – not owner. 404. |
| POST | `/api/events/{eventId}/results` | Captures a Participant's finish time and finishing position after the event has taken place. One result per enrolment; each position used once per event. | Organiser (owner) | `{ enrolmentId, finishTime (hh:mm:ss), finishPosition }` | **201 Created** – result. 400 – invalid time / event not finished. 401. 403 – not owner. 404 – event or enrolment not found. 409 – result or position already exists. |
| GET | `/api/events/{eventId}/results` | Lists the published results of an event ordered by finishing position. | Any (logged in) | None | **200 OK** – list of results. 401. 404. |
| GET | `/api/results/mine` | Returns the logged-in Participant's personal race history: event, category, finish time, position and total finishers. | Participant | None | **200 OK** – list of results. 401. 403 – not a Participant. |

## How the plan covers the brief

| Part 2 functional requirement | Endpoints |
|---|---|
| Authentication – register and log in based on role | register, login, logout |
| User Profile – both roles view and update their own profile | GET/PUT `/api/profile` |
| Events – Organisers manage; both roles view | events endpoints (GET for all, POST/PUT/DELETE for Organiser) |
| Categories – Organisers define; both roles view | categories endpoints |
| Event Enrolments – Participant enrols with a category; Organiser views all enrolments | enrolments endpoints |
| Results – Organiser captures; Participant views own | results endpoints |

## Database design note

The endpoints use the six entities in `ERD.png` (Roles, Users, Events, Categories, Enrolments, Results). `Users.ProfilePictureUrl` and `Events.BannerImageUrl` are nullable columns reserved for the image uploads in Part 3 (Azure Blob Storage); no endpoint uses them in Part 2.
