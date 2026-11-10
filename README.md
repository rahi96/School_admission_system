# School Admission System

A first-come-first-served school admission system. Each class/shift has a limited
number of seats. A parent applies for a seat (the seat is **held** for a short
window), then confirms the admission by paying the fee. If the fee is not paid in
time, the hold expires automatically and the seat is released for others.

The project has a **.NET 10 Web API** backend (PostgreSQL) and a **React 18+
(Vite + TypeScript)** frontend. The whole stack runs with a single
`docker compose up`.

---

## Tech stack

| Layer      | Technology                                             |
|------------|--------------------------------------------------------|
| Backend    | C# / ASP.NET Core (.NET 10), Npgsql (raw SQL)          |
| Database   | PostgreSQL 16                                           |
| Frontend   | React 18+, Vite, TypeScript, react-bootstrap           |
| Tests      | xUnit + Testcontainers (throwaway PostgreSQL)          |
| Packaging  | Docker + Docker Compose                                |

---

## Repository layout

```
.
├── UserApi/            # Backend (.NET Web API)  →  the "backend" deliverable
├── UserApi.Tests/      # Automated backend tests (xUnit + Testcontainers)
├── frontend/           # React + Vite frontend     →  the "frontend" deliverable
├── docker-compose.yml  # Postgres + backend + frontend
├── ASSUMPTIONS.md
├── AI_USAGE.md
└── README.md
```

> Note: the backend folder is named `UserApi` (the API project) rather than
> `backend`. See [ASSUMPTIONS.md](./ASSUMPTIONS.md).

---

## Run with Docker (recommended)

**Prerequisite:** Docker Desktop installed and running. Nothing else is required.

```bash
docker compose up --build
```

This starts three containers:

| Service   | URL / Port                     | Notes                              |
|-----------|--------------------------------|------------------------------------|
| frontend  | http://localhost:3000          | React app served by Nginx          |
| backend   | http://localhost:8080          | REST API                           |
| db        | localhost:5432                 | PostgreSQL (data in a named volume)|

Open **http://localhost:3000** in your browser.

To stop:

```bash
docker compose down        # stop and remove containers (data is kept)
docker compose down -v     # also delete the database volume (wipes all data)
```

Data is stored in the `pgdata` Docker volume, so it **survives**
`docker compose down` followed by `docker compose up` (only `-v` erases it).

### Configuration (environment variables)

| Variable                | Used by  | Default                 | Purpose                                   |
|-------------------------|----------|-------------------------|-------------------------------------------|
| `DATABASE_URL`          | backend  | (set in compose)        | PostgreSQL connection string              |
| `HOLD_DURATION_SECONDS` | backend  | `600`                   | How long a seat hold lasts before expiry  |
| `VITE_API_BASE_URL`     | frontend | `http://localhost:8080` | Backend URL the browser calls (build arg) |

All three are wired in [docker-compose.yml](./docker-compose.yml); change them
there to point at a different database, hold duration, or backend URL.

---

## Run locally without Docker (for development)

**Backend** (needs a reachable PostgreSQL and .NET 10 SDK):

```bash
cd UserApi
# Supply a connection string via either of:
#   setx DATABASE_URL "postgresql://user:pass@host:5432/dbname"
#   dotnet user-secrets set "ConnectionStrings:Default" "<connection string>"
dotnet run
```

The backend listens on **http://localhost:8080**. Swagger UI is available at
`http://localhost:8080/swagger` in the Development environment.

**Frontend:**

```bash
cd frontend
npm install
npm run dev          # http://localhost:5173
```

Set the backend URL in `frontend/.env` (copy from `frontend/.env.example`):

```
VITE_API_BASE_URL=http://localhost:8080
```

---

## API

Base URL: `http://localhost:8080`. All requests/responses are JSON. The caller's
identity is passed in the `X-Student-Id` header (a 17-digit registration number).

| # | Method & Path                      | Description                              |
|---|------------------------------------|------------------------------------------|
| 1 | `POST   /classes`                  | Create a class                           |
| 2 | `GET    /classes`                  | List classes with seat accounting        |
| 3 | `GET    /classes/{classId}`        | One class's seat status                  |
| 4 | `POST   /classes/{classId}/holds`  | Apply for a seat (hold). `409` if full   |
| 5 | `GET    /holds/{holdId}`           | Current status of an application         |
| 6 | `POST   /holds/{holdId}/confirm`   | Pay fee, confirm admission (idempotent)  |
| 7 | `DELETE /holds/{holdId}`           | Cancel an application (`204`)            |
| 8 | `GET    /me/holds`                 | The current student's applications       |

A hold's `status` is one of `HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`.

---

## Running the automated tests

The backend tests use **Testcontainers**, so they start their own throwaway
PostgreSQL container — no configuration and no shared database required.

**Prerequisite:** Docker running + .NET 10 SDK.

```bash
dotnet test
```

There are four tests in [UserApi.Tests](./UserApi.Tests), including the
**mandatory concurrency test** (`Concurrent_Holds_NeverExceed_TotalSeats`) which
fires 200 simultaneous hold requests at a 10-seat class and asserts that exactly
10 succeed, nothing is oversold, and no request ever returns HTTP 500.

---

## Mandatory backend rules (all implemented)

1. `held + confirmed` for a class never exceeds `totalSeats`, under any amount of
   concurrency (enforced with a row lock + a single atomic insert).
2. Hold duration is set by `HOLD_DURATION_SECONDS` (default 600); expired holds
   are released automatically by a background service — no API call needed.
3. A student can hold/confirm at most one active application per class.
4. Only a valid, unexpired `HELD` application can be confirmed.
5. Confirming the same application multiple times increments `confirmedSeats`
   only once (idempotent).
6. No data is lost on restart, including `docker compose down` then `up`.
7. Invalid input returns `4xx`, never `500`.
