# School Admission System

A high-concurrency, first-come-first-served online admission and seat reservation system built with **.NET 10 (C#)**, **PostgreSQL 17**, **React 19**, and **Docker**.

The system enables school staff to create class and shift offerings with fixed seat capacities, while allowing applicants to hold seats, track countdown deadlines, simulate fee payment to confirm admission, and cancel active applications. Concurrency guarantees, data consistency, and zero-overbooking rules are enforced at the database level.

---

## Documentation

- **[`ASSUMPTIONS.md`](ASSUMPTIONS.md)**
- **[`AI_USAGE.md`](AI_USAGE.md)**

---

## Docker Setup Guide

The entire platform—PostgreSQL database, backend API, and frontend web client—runs via Docker Compose without requiring any local runtimes or SDKs.

### Prerequisites

- [Docker Engine](https://docs.docker.com/engine/install/) (24.0+)
- [Docker Compose](https://docs.docker.com/compose/) (v2.20+)

### Running the Application

1. **Clone the repository:**

   ```bash
   git clone <repository-url>
   cd SMP
   ```

2. **(Optional) Configure Environment Variables:**
   A `.env.example` file is included in the project root. Copying it is optional, as `docker-compose.yml` provides sensible defaults out of the box:

   ```bash
   cp .env.example .env
   ```

   | Variable            | Default Value           | Description                                  |
   | ------------------- | ----------------------- | -------------------------------------------- |
   | `POSTGRES_DB`       | `school_admission`      | PostgreSQL database name                     |
   | `POSTGRES_USER`     | `school`                | PostgreSQL username                          |
   | `POSTGRES_PASSWORD` | `school_dev`            | PostgreSQL password                          |
   | `POSTGRES_PORT`     | `5432`                  | Exposed PostgreSQL host port                 |
   | `FRONTEND_PORT`     | `5173`                  | Exposed Frontend host port                   |
   | `FRONTEND_ORIGIN`   | `http://localhost:5173` | Allowed CORS origin for backend API          |
   | `VITE_API_BASE_URL` | `http://localhost:8080` | Backend API URL embedded into frontend build |

3. **Start the containers:**

   ```bash
   docker compose up --build
   ```

   To run in the background (detached mode):

   ```bash
   docker compose up --build -d
   ```

4. **Stop the containers:**
   ```bash
   docker compose down
   ```

### Data Persistence & Migrations

- **Database Persistence:** PostgreSQL data is stored in a named Docker volume (`postgres_data`). Data persists across `docker compose down` and `docker compose up` cycles.
- **Automatic Schema Migration:** On application startup, the backend automatically applies pending Entity Framework Core migrations inside a transaction guarded by a PostgreSQL advisory lock (`pg_advisory_lock(-2)`). This prevents race conditions during schema initialization even if multiple backend instances start simultaneously.

---

## Application URLs

Once the Docker Compose services are running, the application endpoints are available at:

| Service              | URL                                                                          | Description                                                   |
| -------------------- | ---------------------------------------------------------------------------- | ------------------------------------------------------------- |
| **Frontend UI**      | [http://localhost:5173](http://localhost:5173)                               | Single-page React application for applicants and school admin |
| **Backend API**      | [http://localhost:8080](http://localhost:8080)                               | ASP.NET Core Minimal API root                                 |
| **API Health Check** | [http://localhost:8080/health](http://localhost:8080/health)                 | Returns `{ "status": "healthy" }`                             |
| **Real-time Stream** | [http://localhost:8080/classes/stream](http://localhost:8080/classes/stream) | Server-Sent Events (SSE) feed for live class seat updates     |

---

## Test Suite Summary & Overview

The repository contains **42 automated tests** across the backend and frontend suites, ensuring comprehensive verification of business logic, database constraints, high-concurrency race conditions, RFC 7807 error masking, and client-side state persistence:

| Test Suite | Framework | Total Tests | Status | Key Coverage Areas |
|---|---|---|---|---|
| **Backend Integration & Concurrency** | xUnit, ASP.NET Core 10, PostgreSQL 17 | **37** | ✅ Passing | 50-thread concurrency barrier, row-level locks, CHECK constraints, hold expiry, idempotent confirmations, ownership concealment (404), RFC 7807 error masking |
| **Frontend Unit & Component** | Vitest, React Testing Library, JSDOM | **5** | ✅ Passing | 17-digit student ID persistence in `localStorage`, API error handling, client-side expiry countdown calculations |
| **Total Automated Tests** | — | **42** | ✅ Passing | **100% pass rate** across all suites |

### Test Breakdown by Category

- **Backend Integration & Concurrency (`backend/SchoolAdmission.Api.Tests` — 37 tests):**
  - **Class Management & Constraints (`ClassesFlowTests.cs` — 19 tests):** Class creation, trimmed name normalization, case-insensitive uniqueness (`normalized_name`), PostgreSQL schema CHECK constraints (`held_seats + confirmed_seats <= total_seats`, non-negative counters), boundary value validation (1–100 chars, 1–1,000 seats), and malformed JSON payload rejection.
  - **Seat Holds & Concurrency State Machine (`HoldsFlowTests.cs` — 16 tests):** Seat reservation, payment confirmation idempotency, hold cancellation, expired hold rejection (409 Conflict), student ownership enforcement (404 concealment per ADR-11), single global active application rule (`uq_student_active`), and a 50-thread concurrent barrier race condition test verifying zero overbooking.
  - **Error Schema & Masking (`ErrorContractTests.cs` — 2 tests):** RFC 7807 `ProblemDetails` schema compliance and validation that database exception details and stack traces are suppressed in both Development and Production environments.
- **Frontend Component & State Tests (`frontend/src/features/holds/hold.test.tsx` — 5 tests):**
  - `StudentIdProvider` context initialization with empty state.
  - 17-digit birth registration number persistence to `localStorage`.
  - Session clearing on student ID reset.
  - `ApiError` class creation and HTTP status mapping.
  - Client-side countdown calculation and expired state detection.

---

## Running Tests

### 1. Backend Automated & Concurrency Tests (Docker)

The backend test suite runs against an isolated, ephemeral PostgreSQL 17 container defined in `docker-compose.tests.yml`. Each test execution creates and drops its own disposable database (`admission_test_*`) without touching development data or persistent volumes.

Run the entire test suite:

```bash
docker compose -p smp-tests -f docker-compose.tests.yml up --build --abort-on-container-exit --exit-code-from tests
```

Run a specific test fixture (e.g., concurrency & holds workflow):

```bash
docker compose -p smp-tests -f docker-compose.tests.yml run --rm tests dotnet test SchoolAdmission.sln --filter FullyQualifiedName~HoldsFlowTests
```

Run class creation and uniqueness tests:

```bash
docker compose -p smp-tests -f docker-compose.tests.yml run --rm tests dotnet test SchoolAdmission.sln --filter FullyQualifiedName~ClassesFlowTests
```

Tear down test containers:

```bash
docker compose -p smp-tests -f docker-compose.tests.yml down
```

### 2. Concurrency Test Implementation

The test suite includes a strict concurrency verification test (`ConcurrentHoldRequests_NeverExceedCapacity` in `HoldsFlowTests.cs`):

- Creates a class offering with a strict capacity of **5 seats**.
- Spawns **50 concurrent requests** from 50 distinct student IDs synchronized using a thread barrier (`System.Threading.Barrier`).
- Asserts that exactly **5 requests succeed** with HTTP `201 Created`, and the remaining **45 requests are rejected** with HTTP `409 Conflict`.
- Verifies database state consistency: `heldSeats = 5`, `availableSeats = 0`, with zero overbooking.

### 3. Running Backend Tests Locally (.NET SDK)

If you have .NET 10 SDK and a local PostgreSQL instance running:

```bash
# Set connection string to a disposable test database
export TEST_DATABASE_CONNECTION="Host=localhost;Port=5432;Database=admission_test_admin;Username=school;Password=school_dev"

# Run tests
dotnet test backend/SchoolAdmission.sln
```

### 4. Frontend Checks

To run TypeScript verification, linter, and frontend tests locally:

```bash
pnpm --dir frontend build     # TypeScript type-check and Vite production build
pnpm --dir frontend lint      # Oxlint source code linting
pnpm --dir frontend test      # Vitest unit & component test suite
```

---

## Architecture & Technical Decisions

### 1. Concurrency Control & Zero Overbooking

Preventing race conditions when hundreds or thousands of parents apply simultaneously for limited seats is the core technical challenge.

- **Row-Level Locking (`SELECT ... FOR UPDATE`):** When processing a hold creation (`POST /classes/{classId}/holds`), the backend begins a transaction and locks the specific class row using `SELECT * FROM classes WHERE id = @id FOR UPDATE`. This serializes concurrent requests targeting the same class. Each transaction takes ~2–5 ms, maintaining predictable serialization without blocking requests for other classes.
- **Database CHECK Constraint (Defense-in-Depth):** Application logic validates capacity before incrementing `held_seats`. As a second fail-safe layer, PostgreSQL enforces a table constraint:
  ```sql
  CONSTRAINT ck_classes_capacity CHECK (held_seats + confirmed_seats <= total_seats)
  ```
  Even in the event of an unhandled application bug, the database engine guarantees that overbooking is physically impossible.
- **Multi-Process Concurrency Safety:** Because serialization relies on PostgreSQL row locks and ACID transactions rather than in-memory locks, multiple backend API replicas running behind a load balancer safely share the same database without race conditions.

### 2. Counter-Based Seat Tracking vs. Creating Seats Beforehand

A critical architectural decision was whether to pre-generate individual seat records in the database (the cinema/airline model) or maintain atomic numeric counters on the class record.

**Decision: Counter-Based Seat Allocation.**

- **Domain Nature (Fungibility):** In a cinema or airline booking system, seats are non-fungible physical assets (e.g., Seat 12A vs. Seat 14B). In school admissions, seats within a class shift are completely interchangeable quotas. Students do not choose specific desks or positions during registration.
- **Performance & Overhead:** Pre-generating hundreds or thousands of individual seat rows per class shift would result in table bloat, index overhead, and complex reservation locking (e.g., `FOR UPDATE SKIP LOCKED`).
- **Atomicity & Simplicity:** Managing `held_seats`, `confirmed_seats`, and `total_seats` directly on the class row—guarded by row-level locking and database `CHECK` constraints—delivers higher throughput, minimal storage overhead, and clean mathematical invariants.

### 3. Hold Expiry & Dual-Layer Reclamation

Seat holds expire automatically if payment is not completed within `HOLD_DURATION_SECONDS` (default: 600 seconds / 10 minutes).

- **Background Sweep Worker (`IHostedService`):** A background service (`HoldExpiryWorker`) wakes up every 10 seconds and batch-updates overdue holds from `HELD` to `EXPIRED`, decrementing the corresponding `held_seats` count.
- **Advisory Lock for Multi-Instance Deployments:** The worker uses `pg_try_advisory_lock(1)` to ensure that only one backend instance executes the sweep at any given time, preventing redundant database updates across replicas.
- **Time-Aware Reads (Immediate Consistency):** To eliminate reliance on the background worker's cadence, all read queries and business mutations evaluate `expires_at` dynamically against `DateTimeOffset.UtcNow`. If a hold's deadline has passed, the system immediately treats it as expired, ensuring real-time consistency even between sweep intervals.

### 4. Global Single-Admission Constraint

The specification restricts students from holding duplicate seats. To prevent seat hoarding across multiple shifts or classes, a student is restricted to at most one active (`HELD` or `CONFIRMED`) application across the entire institution.

- **PostgreSQL Partial Unique Index:**
  ```sql
  CREATE UNIQUE INDEX uq_student_active
  ON holds (student_id)
  WHERE status IN ('HELD', 'CONFIRMED');
  ```
  Attempting to acquire a second hold while another hold or confirmed admission is active triggers a unique violation, which the API cleanly maps to HTTP `409 Conflict`.

### 5. Idempotent State Machine

Holds transition through a one-way state machine:

```
[Available Seat] ──> HELD ──┬──(Payment Confirmed)──> CONFIRMED (Terminal)
                            ├──(User Cancelled)─────> CANCELLED (Terminal)
                            └──(Time Expired)───────> EXPIRED   (Terminal)
```

- **Idempotent Confirmations:** Submitting duplicate confirmation requests (`POST /holds/{holdId}/confirm`) on an already `CONFIRMED` admission returns HTTP `200 OK` with the current state and does not increment `confirmed_seats` again.
- **Confirmed Admission Immutability:** Once an admission is confirmed, it cannot be cancelled (`DELETE /holds/{holdId}` returns `409 Conflict`), preserving admission integrity.
- **Immediate Cancellation:** Cancelling an active `HELD` admission immediately releases the held seat back to the class's available pool and sets `cancelled_at`.

### 6. Real-Time Seat Synchronization

- **PostgreSQL LISTEN / NOTIFY:** Whenever hold mutations (creation, confirmation, cancellation, or expiry) occur, a PostgreSQL notification is broadcast (`NOTIFY admission_classes`).
- **Server-Sent Events (SSE):** The backend listens for database notifications and pushes updated seat counts over an HTTP stream (`GET /classes/stream`) using a single long-lived connection.
- **Client Fallback & Reconciliation:** If the SSE stream disconnects, the frontend automatically falls back to periodic 3-second polling until reconnection succeeds.
- **Client-Side Expiry Countdown:** Expiry timers on active holds are computed client-side from the static `expiresAt` timestamp, ticking down second-by-second without generating unnecessary server traffic.

### 7. Information Hiding & Error Architecture

- **RFC 7807 Problem Details:** All API errors adhere to the standard `ProblemDetails` schema (`type`, `title`, `status`, `detail`).
- **Secure Error Responses:** Database error codes, connection strings, and stack traces are suppressed across all environments.
- **Resource Ownership Concealment:** If an applicant attempts to access or mutate a hold belonging to a different student, the API returns HTTP `404 Not Found` rather than `403 Forbidden`, preventing student ID enumeration attacks.

---

## API Summary

All endpoints accept and return JSON. Authenticated requests use the `X-Student-Id` header (17-digit birth registration number).

| Method   | Endpoint                   | Auth Header    | Description                                           |
| -------- | -------------------------- | -------------- | ----------------------------------------------------- |
| `POST`   | `/classes`                 | None           | Create a new class/shift offering                     |
| `GET`    | `/classes`                 | None           | List all classes with seat statistics                 |
| `GET`    | `/classes/{classId}`       | None           | Get real-time seat statistics for a class             |
| `GET`    | `/classes/stream`          | None           | Server-Sent Events stream for live class seat updates |
| `POST`   | `/classes/{classId}/holds` | `X-Student-Id` | Reserve a seat (creates a temporary hold)             |
| `GET`    | `/holds/{holdId}`          | `X-Student-Id` | Get current status and expiry of a hold               |
| `POST`   | `/holds/{holdId}/confirm`  | `X-Student-Id` | Confirm admission (simulates fee payment)             |
| `DELETE` | `/holds/{holdId}`          | `X-Student-Id` | Cancel an active hold and release the seat            |
| `GET`    | `/me/holds`                | `X-Student-Id` | List all current and historical holds for the student |
| `GET`    | `/health`                  | None           | System health check probe                             |

---

## Features

- **Class & Shift Administration:** Create distinct class and shift offerings (e.g., "Class 6 - Morning Shift", "Class 6 - Day Shift") with custom seat capacities. Class names are validated, trimmed, and checked for case-insensitive uniqueness.
- **Live Seat Availability Catalogue:** View total, held, confirmed, and available seat counts across all classes, updating live without manual page reloads.
- **First-Come-First-Served Reservation:** Reserve a seat instantly with automated hold creation and configurable reservation duration (default: 10 minutes).
- **Interactive Countdown Timer:** Real-time visual countdown on active holds indicating remaining time to complete fee payment.
- **Simulated Fee Payment & Instant Confirmation:** One-click fee submission that confirms admission and secures the seat permanently.
- **Hold Cancellation:** Voluntarily release held seats prior to expiry so other applicants can apply immediately.
- **Automatic Seat Reclamation:** Expired holds are automatically reclaimed by the background sweeper and query-level time filters.
- **Application History ("My Applications"):** Chronological dashboard showing all active and past applications (`HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`) for the entered student ID.
- **Frictionless Identity (Zero Login):** Seamless user experience using the 17-digit birth registration number (`X-Student-Id`), saved in `localStorage` for session persistence across browser restarts.
- **Resilient UI Error Handling:** User-friendly alerts for seat exhaustion, expired holds, network disconnects, and duplicate application attempts.
- **Complete Containerization:** Single-command startup with Docker Compose, including persistent volume storage and automated database migrations.
