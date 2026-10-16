
# RaceDay System — API Endpoint Plan

## Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Creates a new user account as either an Organiser or a Participant. | None (public) | `{ fullName, email, password, role, phone }` | 201 Created – new user object (no password) <br> 409 Conflict – email already registered |
| POST | /api/auth/login | Authenticates a user and returns a session/JWT token. | None (public) | `{ email, password }` | 200 OK – token + user profile <br> 401 Unauthorized – invalid credentials |

## User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/users/me | Returns the profile of the currently logged-in user. | Any (logged in) | None | 200 OK – user profile <br> 401 Unauthorized – no/invalid token |
| PUT | /api/users/me | Updates the logged-in user's own profile details. | Any (logged in) | `{ fullName, phone }` | 200 OK – updated profile <br> 400 Bad Request – invalid data |

## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events | Lists all events, optionally filtered by status/date. | None (public) | None | 200 OK – array of events |
| GET | /api/events/{id} | Returns full details for a single event. | None (public) | None | 200 OK – event object <br> 404 Not Found – event does not exist |
| POST | /api/events | Creates a new event owned by the logged-in Organiser. | Organiser | `{ eventName, eventDate, location, description }` | 201 Created – new event <br> 400 Bad Request – missing/invalid fields |
| PUT | /api/events/{id} | Updates an existing event's details or status. | Organiser (owner) | `{ eventName, eventDate, location, description, status }` | 200 OK – updated event <br> 403 Forbidden – not the owner <br> 404 Not Found – event does not exist |
| DELETE | /api/events/{id} | Deletes an event and its categories. | Organiser (owner) | None | 200 OK – event deleted <br> 403 Forbidden – not the owner <br> 404 Not Found – event does not exist |

## Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events/{id}/categories | Lists all race categories for a specific event. | None (public) | None | 200 OK – array of categories <br> 404 Not Found – event does not exist |
| POST | /api/events/{id}/categories | Adds a new race category (e.g. 10km, 21km) to an event. | Organiser (owner) | `{ categoryName, distanceKm, entryFee, maxParticipants }` | 201 Created – new category <br> 403 Forbidden – not the owner <br> 404 Not Found – event does not exist |
| PUT | /api/categories/{id} | Updates a race category's details. | Organiser (owner) | `{ categoryName, distanceKm, entryFee, maxParticipants }` | 200 OK – updated category <br> 403 Forbidden – not the owner <br> 404 Not Found – category does not exist |
| DELETE | /api/categories/{id} | Removes a race category. | Organiser (owner) | None | 200 OK – category deleted <br> 403 Forbidden – not the owner <br> 404 Not Found – category does not exist <br> 409 Conflict – category already has enrolments |

## Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/categories/{id}/enrol | Enrols the logged-in Participant into a race category. | Participant | None (uses logged-in user) | 201 Created – enrolment record <br> 404 Not Found – category does not exist <br> 409 Conflict – already enrolled or category full |
| GET | /api/users/me/enrolments | Lists all enrolments belonging to the logged-in Participant. | Participant | None | 200 OK – array of enrolments |
| DELETE | /api/enrolments/{id} | Cancels an enrolment before the event date. | Participant (owner) | None | 200 OK – enrolment cancelled <br> 403 Forbidden – not the owner <br> 404 Not Found – enrolment does not exist |
| GET | /api/categories/{id}/enrolments | Lists all participants enrolled in a category. | Organiser (owner) | None | 200 OK – array of enrolments <br> 403 Forbidden – not the owner <br> 404 Not Found – category does not exist |

## Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/enrolments/{id}/result | Captures a finish time/position/status for a participant's enrolment. | Organiser (owner) | `{ finishTime, position, status }` | 201 Created – result record <br> 403 Forbidden – not the owner <br> 404 Not Found – enrolment does not exist |
| PUT | /api/results/{id} | Corrects an existing result. | Organiser (owner) | `{ finishTime, position, status }` | 200 OK – updated result <br> 403 Forbidden – not the owner <br> 404 Not Found – result does not exist |
| GET | /api/events/{id}/results | Returns the full results/leaderboard for an event. | None (public) | None | 200 OK – array of results <br> 404 Not Found – event does not exist |

---
