# School Admission System

## Run the application

Start PostgreSQL with the repository Compose setup, then run the backend and frontend from separate terminals:

```powershell
dotnet run --project backend/SchoolAdmission.Api
pnpm --dir frontend dev --host 0.0.0.0
```

The API listens on `http://localhost:8080` when launched through Compose, and the Vite development server defaults to `http://localhost:5173`. Set `ConnectionStrings__Database` for the API and `VITE_API_BASE_URL` for a frontend hosted separately from the API. EF migrations run when the API starts.

## Run backend integration tests

The tests require Docker and use an isolated PostgreSQL 17 service with no persistent volume:

```powershell
docker compose -p smp-tests -f docker-compose.tests.yml up --build --abort-on-container-exit --exit-code-from tests
docker compose -p smp-tests -f docker-compose.tests.yml run --rm tests dotnet test SchoolAdmission.sln --filter FullyQualifiedName~ClassesFlowTests
docker compose -p smp-tests -f docker-compose.tests.yml down
```

Each test creates and drops only its own randomly named `admission_test_*` database. To run tests with a host .NET SDK, set `TEST_DATABASE_CONNECTION` to a dedicated disposable PostgreSQL server and run `dotnet test backend/SchoolAdmission.sln`.

Frontend checks:

```powershell
pnpm --dir frontend build
pnpm --dir frontend lint
```
