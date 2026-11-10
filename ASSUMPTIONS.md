# Assumptions

Decisions made where the brief did not specify, and the reasoning behind them.

## Folder / project names
- The backend project is `UserApi/` and the test project is `UserApi.Tests/`,
  rather than a folder literally named `backend/`. The project was scaffolded
  from an existing API template; the name was kept to preserve commit history.
  All backend code and its Dockerfile live under `UserApi/`.

## Identity
- A student is identified only by the 17-digit registration number sent in the
  `X-Student-Id` header, as described in the brief. There is no login.
- The frontend stores the entered registration number in `localStorage` so the
  student still sees their applications after a page refresh or after closing and
  reopening the browser.
- The number is validated on the client as exactly 17 digits. The backend does
  not enforce a strict format (it treats any non-empty header value as an
  identity), to stay liberal in what it accepts while the UI guides the user.

## IDs
- Class and hold IDs are server-generated opaque strings (`c_<guid>`, `h_<guid>`).
  The brief states the example IDs (`c_123`, `h_789`) are illustrative and the
  format is our choice.

## Seats and concurrency
- "Occupied" seats = confirmed holds + holds that are `HELD` and not yet expired.
  Expired and cancelled holds free their seat immediately.
- The no-overselling rule is enforced inside a database transaction: the class
  row is locked (`SELECT ... FOR UPDATE`) and the seat check + insert happen as a
  single atomic SQL statement. This guarantees correctness regardless of how many
  requests arrive at once, and also satisfies the bonus (two backend processes on
  one database stay correct, because the guarantee lives in the database, not in
  app memory).
- Under an extreme simultaneous burst, a request that cannot acquire its lock in
  time returns **HTTP 503** ("busy, try again") rather than 500. This honours the
  "never 500" rule while keeping the seat guarantee. Normal rejections (no seat,
  duplicate application) return **409**.

## Hold expiry
- Expiry is driven by the database timestamp `expires_at`. Reads treat a `HELD`
  row past its expiry as `EXPIRED` even before it is swept, so the API never
  reports a stale `HELD`.
- A background service also sweeps expired holds every few seconds and flips their
  status to `EXPIRED`, so seat counts free up without anyone calling an API.
- The frontend shows a live countdown and flips a row to `EXPIRED` (disabling the
  "Pay" button) the moment the countdown reaches zero, without a page reload.

## Confirm / cancel semantics
- Confirm is idempotent: confirming an already-confirmed hold returns `200` and
  does not increment `confirmedSeats` again.
- Confirm/cancel can only be performed by the hold's owner (verified against
  `X-Student-Id`); another student gets `403`.
- Cancelling an already expired/cancelled hold is treated as success (idempotent).
- "Pay admission fee" assumes payment succeeded; there is no real payment gateway,
  as allowed by the brief.

## Payment
- No real payment integration. Pressing "Pay admission fee" directly confirms the
  hold, per the brief.

## Database
- For Docker, the stack runs its own PostgreSQL 16 container with a named volume
  (`pgdata`) so data survives `docker compose down` / `up`. This is what the brief
  intends and works on any machine without external credentials.
- The backend accepts a connection string either as a standard Npgsql key/value
  string or as a `postgres://` / `postgresql://` URL (common with managed
  providers), normalising the URL form before use.

## Frontend
- The UI polls the backend every 3 seconds so seat counts and application statuses
  update without a manual reload (simple and robust; no websocket was required).
- react-bootstrap was chosen for quick, clean, accessible components. Visual
  polish was kept minimal in favour of correct, easy-to-use behaviour.

## Ports
- Backend: `8080`. Frontend (Docker/Nginx): `3000`. Frontend (local Vite dev):
  `5173`. PostgreSQL: `5432`.
- On the development machine, port `8080` can be occupied by other local software
  (e.g. an Apache instance). In Docker this is a non-issue; for local `dotnet run`
  the port is configurable via `ASPNETCORE_URLS` / launch settings.
