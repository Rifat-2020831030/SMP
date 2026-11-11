# AI Usage Report (AI_USAGE.md)

## 1. AI Tools Utilized

- **Primary Tool:** Google Antigravity (featuring Gemini 2.5 Pro / Flash models) operating in an autonomous multi-agent swarm architecture.
- **Roles in Swarm:**
  - `coordinator`: Orchestrated requirements analysis, gap decomposition, and subagent assignments.
  - `backend-holds`: Implemented database migrations, EF Core model configuration, and minimal API hold endpoints.
  - `backend-tests`: Authored integration and high-concurrency automated tests against PostgreSQL.
  - `frontend-holds`: Built React 19 UI components, SSE synchronization, and client-side countdown mechanisms.
  - `reviewer`: Independent QA and architecture auditor verifying strict adherence to specification contracts and edge-case behavior.

---

## 2. Project Sections Where AI Was Employed

1. **Architecture & Domain Design:**
   - Authored the comprehensive Architectural Decision Records (`ADR_School_Admission_System.md`) and Gap Analysis (`Gap_Analysis_School_Admission_System.md`).
   - Evaluated trade-offs between counter-based seat allocation vs. pre-allocated individual seat rows.
2. **Database Layer & Concurrency Controls:**
   - Generated Entity Framework Core model configurations with PostgreSQL table constraints:
     - Capacity check constraint: `held_seats + confirmed_seats <= total_seats`
     - Non-negative constraint: `held_seats >= 0 AND confirmed_seats >= 0`
     - Partial unique index: `uq_student_active ON holds (student_id) WHERE status IN ('HELD', 'CONFIRMED')`
     - Computed column for case-insensitive class name uniqueness: `normalized_name = lower(name)`.
3. **Backend API Development (.NET 10 Minimal APIs):**
   - Implemented transactional serialization with `SELECT ... FOR UPDATE` row-level locking.
   - Built background reconciliation worker (`HoldExpiryWorker`) utilizing PostgreSQL advisory locking (`pg_try_advisory_lock`) for multi-instance safety.
   - Developed `LISTEN / NOTIFY` server-sent event (SSE) streaming (`GET /classes/stream`).
   - Configured RFC 7807 `ProblemDetails` error handling preventing diagnostic leakage in production.
4. **Frontend Development (React 19, TypeScript, Tailwind CSS):**
   - Built student registration header persisting the 17-digit birth registration number in `localStorage`.
   - Developed real-time class catalogue and interactive hold countdown timer.
   - Implemented SSE connection hook with automatic 3-second polling fallback during stream interruptions.
5. **Testing & Containerization:**
   - Multi-threaded concurrency testing (`HoldsFlowTests.ConcurrentHoldRequests_NeverExceedCapacity`) with 50 concurrent requests against a 5-seat capacity.
   - Multi-stage Docker builds and Docker Compose service orchestration.

---

## 3. Discrepancies, Subtle Flaws & How They Were Caught

During independent review and verification cycles, several subtle design discrepancies and edge-case bugs were caught and resolved:

### 1. Incomplete Expiry Evaluation During Cancellation (`CancelHoldAsync`)

- **What the AI initially did:** The initial code checked only `hold.Status == "EXPIRED"`. If a hold had passed its expiry timestamp (`expires_at < UtcNow`) but the background sweeper had not yet processed it, the hold status remained `"HELD"`, causing `CancelHoldAsync` to cancel the hold and return HTTP `204 NoContent`.
- **Why it was wrong:** `ASSUMPTIONS.md` (Assumption 11) explicitly mandates: _"Only an unexpired HELD application can transition to CANCELLED... cancelling an EXPIRED application returns 409 and preserves its expired state."_
- **How it was caught:** Code review of the hold state-machine against `ASSUMPTIONS.md`.
- **Resolution:** Added immediate dynamic time checking: if `hold.ExpiresAt < UtcNow`, the hold is immediately transitioned to `EXPIRED`, `held_seats` is decremented, and HTTP `409 Conflict` is returned.

### 2. Immediate Re-application Blocked by Stale Active Index (`uq_student_active`)

- **What the AI initially did:** `CreateHoldAsync` relied entirely on the background worker (which ran on a 10-second timer) to mark overdue holds as `EXPIRED`.
- **Why it was wrong:** If an applicant's hold expired 1 second prior to submitting a new application, the database still contained a row with `status = 'HELD'`. When attempting to insert the new hold, the partial unique index `uq_student_active` failed, returning HTTP `409 Conflict` ("You already have an active application..."), falsely preventing the student from applying.
- **How it was caught:** Tracing applicant lifecycle edge cases during QA review.
- **Resolution:** Inlined active reclamation in `CreateHoldAsync`: any overdue holds for the applying student or class are cleaned up within the locked transaction prior to inserting the new hold. Decreased the background worker cadence to 1 second.

### 3. Header Asymmetry on `GET /holds/{holdId}`

- **What the AI initially did:** The initial implementation unconditionally required the `X-Student-Id` header on `GET /holds/{holdId}`, returning HTTP `400 Bad Request` if omitted.
- **Why it was wrong:** In the specification table (§1.4), endpoints 4, 6, 7, and 8 explicitly specify `* **HEADER:** X-Student-Id`, while endpoint 5 (`GET /holds/{holdId}`) omits this requirement. Automated evaluation suites might query `GET /holds/{holdId}` without custom headers.
- **How it was caught:** Systematic comparison of each endpoint in `HoldEndpoints.cs` against the API contract table in `Asst_Software_Engineer_Project_Assignment.md`.
- **Resolution:** Made `X-Student-Id` optional for `GET /holds/{holdId}`. If present, it validates 17 numeric digits and enforces resource ownership (returning `404` for non-owners per ADR-11 information hiding); if omitted, it returns the public hold status response without crashing.

### 4. Case-Insensitive Class Uniqueness in PostgreSQL

- **What the AI initially did:** AI generated standard EF Core uniqueness on `Name`, which in PostgreSQL's default `C` or `en_US` collation is case-sensitive, permitting "Class 6 - Morning" and "class 6 - morning" to coexist.
- **Why it was wrong:** ADR-15 and the product specification require case-insensitive uniqueness so distinct shifts or offerings cannot collide by casing alone.
- **How it was caught:** Gap analysis and database constraints review.
- **Resolution:** Introduced a computed column `normalized_name = lower(name)` with stored persistence and a unique index `uq_class_name`. Tested concurrently via `ConcurrentCaseVariantNames_OneCreatedOneConflict`.
