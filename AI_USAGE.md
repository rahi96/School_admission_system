# AI Usage

## Tools used
- An AI coding assistant (GitHub Copilot) was used throughout, inside the editor,
  for design discussion, writing code, and generating tests and documentation.

## Where it was used
- **Backend API**: scaffolding the controllers, the raw-SQL data access, and the
  concurrency-safe hold logic (row lock + single atomic insert).
- **Frontend**: the React + TypeScript components (class list, create-class,
  my-applications with live countdown), the API client, and the identity hook.
- **Tests**: the xUnit + Testcontainers setup and the four test cases, including
  the concurrency test.
- **Docker**: the backend and frontend Dockerfiles, the Nginx config, and
  `docker-compose.yml`.
- **Docs**: drafts of `README.md`, `ASSUMPTIONS.md`, and this file.

## Where the AI was wrong, and how it was caught
1. **Oversold-safe but 500-prone first version.** The first implementation of the
   "create hold" path made several separate database round-trips while holding the
   class row lock. It was correct about seats (never oversold), but under a 200-
   request simultaneous burst it exhausted the database connection pool and
   returned some **HTTP 500s** — a violation of the "never 500" rule. This was
   caught by actually running a concurrency load test and reading the server logs
   (which showed `connection pool has been exhausted`). Fix: collapse the logic
   into a single atomic SQL statement, catch transient DB errors and return
   **503** instead, and add a global exception handler as a safety net.

2. **Connection-string SSL bug.** The helper that parses `postgres://` URLs
   initially forced `SslMode=Require` for every URL (correct for the cloud dev
   database, wrong for the local Docker PostgreSQL, which has no SSL). This was
   caught when wiring up Docker. Fix: only default to `Require` when the URL does
   not explicitly set `sslmode`, so a local `sslmode=disable` URL is honoured.

3. **Over-strict concurrency assertion.** The first version of the concurrency
   test asserted that every non-granted request returned exactly `409`. Running it
   showed some requests legitimately returned `503` (busy) under load. The test
   was corrected to assert the real invariants: exactly `totalSeats` succeed,
   `held + confirmed` never exceeds `totalSeats`, and no response is ever `500`.

4. **Test host configuration.** The first attempt to inject the test database
   connection string via `ConfigureAppConfiguration` did not take effect before
   the app read it at startup, so the app threw "no connection string". Fix: use
   `builder.UseSetting(...)` so the value is present when the host builds.

## How AI output was verified
- The backend was run and every endpoint smoke-tested; all six mandatory rules
  were verified with live requests (including auto-expiry and persistence across a
  full restart).
- The concurrency guarantee was verified both by an ad-hoc load test and by the
  automated `dotnet test` suite.
- The full Docker stack was built and run, and data persistence across
  `docker compose down` / `up` was confirmed.
- The frontend flow (apply → countdown → pay → confirmed) was exercised in the
  browser against the running backend.
