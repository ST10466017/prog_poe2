# RaceDay System — API Endpoint Plan (v2)

Roles: **None** (public), **Any** (any logged-in user), **Organiser**, **Participant**.
"(owner)" = the endpoint also checks the logged-in Organiser owns the event, or the Participant owns the enrolment.
Authentication is session based: `POST /api/auth/login` starts a server-side session (cookie) that stores the user id and role.

## Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Creates an account as an Organiser or a Participant (only these two roles are accepted; password is hashed). | None (public) | `{ fullName, email, password, role, phone }` | 201 Created – user (no password) <br> 400 Bad Request – invalid data / unknown role <br> 409 Conflict – email already registered |
| POST | /api/auth/login | Verifies credentials and starts a session holding the user id and role. | None (public) | `{ email, password }` | 200 OK – user profile <br> 401 Unauthorized – invalid credentials |
| POST | /api/auth/logout | Ends the current session. | Any (logged in) | None | 200 OK – logged out <br> 401 Unauthorized |

## User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/users/me | Returns the logged-in user's own profile. | Any (logged in) | None | 200 OK – profile <br> 401 Unauthorized |
| PUT | /api/users/me | Updates the logged-in user's own name and phone. | Any (logged in) | `{ fullName, phone }` | 200 OK – updated profile <br> 400 Bad Request <br> 401 Unauthorized |

## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events | Lists events; optional `eventType` and `status` filters. | None (public) | None | 200 OK – array of events <br> 400 Bad Request – invalid filter |
| GET | /api/events/{id} | Returns one event (name, description, date, location, distance, event type). | None (public) | None | 200 OK – event <br> 404 Not Found |
| POST | /api/events | Creates an event owned by the logged-in Organiser. | Organiser | `{ eventName, description, eventDate, location, distanceKm, eventType (Run/Walk/Cycle) }` | 201 Created – event <br> 400 Bad Request <br> 401 / 403 |
| PUT | /api/events/{id} | Updates an event (including its status). | Organiser (owner) | `{ eventName, description, eventDate, location, distanceKm, eventType, status }` | 200 OK – event <br> 400 / 401 / 403 Forbidden – not owner <br> 404 Not Found |
| DELETE | /api/events/{id} | Deletes an event and its categories. | Organiser (owner) | None | 200 OK – deleted <br> 401 / 403 <br> 404 Not Found <br> 409 Conflict – event has enrolments |

## Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events/{id}/categories | Lists the age/distance categories of an event. | None (public) | None | 200 OK – array <br> 404 Not Found |
| POST | /api/events/{id}/categories | Adds an age or distance category (e.g. Under 20, Senior, 10km, 21km). | Organiser (owner) | `{ categoryName, categoryType (Age/Distance), distanceKm, entryFee, maxParticipants }` | 201 Created – category <br> 400 / 401 / 403 <br> 404 Not Found <br> 409 Conflict – duplicate name in event |
| PUT | /api/categories/{id} | Updates a category. | Organiser (owner) | `{ categoryName, categoryType, distanceKm, entryFee, maxParticipants }` | 200 OK – category <br> 400 / 401 / 403 <br> 404 Not Found <br> 409 Conflict – duplicate name, or limit below current enrolments |
| DELETE | /api/categories/{id} | Removes a category. | Organiser (owner) | None | 200 OK – deleted <br> 401 / 403 <br> 404 Not Found <br> 409 Conflict – has enrolments |

## Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/categories/{id}/enrol | Enrols the logged-in Participant in a category (records participant, event and category) and creates a pending payment for the entry fee. | Participant | `{ paymentMethod }` – optional (Card/EFT/Cash, default EFT) | 201 Created – enrolment <br> 400 / 401 / 403 <br> 404 Not Found – category <br> 409 Conflict – already enrolled in this event, category full, or event not open |
| GET | /api/users/me/enrolments | Lists the Participant's own enrolments. | Participant | None | 200 OK – array <br> 401 / 403 |
| DELETE | /api/enrolments/{id} | Cancels an enrolment (before the event date). | Participant (owner) | None | 200 OK – cancelled <br> 401 / 403 <br> 404 Not Found <br> 409 Conflict – already cancelled, event passed, or result recorded |
| GET | /api/categories/{id}/enrolments | Lists participants enrolled in one category. | Organiser (owner) | None | 200 OK – array <br> 401 / 403 <br> 404 Not Found |
| GET | /api/events/{id}/enrolments | Lists all enrolments for an event. | Organiser (owner) | None | 200 OK – array <br> 401 / 403 <br> 404 Not Found |

## Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/enrolments/{id}/result | Records finish time and position for an enrolment. | Organiser (owner) | `{ finishTime, position, status (Finished/DNF/DNS/Not Started) }` | 201 Created – result <br> 400 / 401 / 403 <br> 404 Not Found <br> 409 Conflict – result exists or enrolment cancelled |
| PUT | /api/results/{id} | Corrects an existing result. | Organiser (owner) | `{ finishTime, position, status }` | 200 OK – result <br> 400 / 401 / 403 <br> 404 Not Found |
| GET | /api/events/{id}/results | Public leaderboard for an event, ordered by position. | None (public) | None | 200 OK – array <br> 404 Not Found |
| GET | /api/users/me/results | Lists the Participant's own results. | Participant | None | 200 OK – array <br> 401 / 403 |

---
**Changes from the first draft (Part 1 v1):** added `distanceKm`/`eventType` to events and `categoryType` to categories (Part 2 brief); added `POST /api/auth/logout`, `GET /api/events/{id}/enrolments` and `GET /api/users/me/results`; login uses a session instead of a JWT; one category per event per participant; extra 409 cases on event delete and enrolment cancel.
