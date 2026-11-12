# Sage Admin

Three application folders (do not add more without approval):

| Folder | What | Stack |
|--------|------|-------|
| `sage-common-lib/` | Shared library (responses, exceptions, DB access, security, validation, logging) reusable by the future User Panel | .NET 10 class library |
| `backend/` | Admin API (`src/`), SQL scripts (`database/`), tests (`Tests/`) | ASP.NET Core, .NET 10, SQL Server |
| `frontend/` | Admin Panel UI | Next.js (App Router), React, TypeScript, Tailwind |

Design and plan: [SAGE_ANALYSIS.md](SAGE_ANALYSIS.md). Current status: **Sprint 4 (Permission Groups) implemented. Next: Sprint 5 (Admin Users).**

## First-time local setup

1. Create and populate the dev database (SQL Server, Windows auth; default instance `localhost\SQLEXPRESS`):
   ```powershell
   cd backend\database\Scripts
   $env:SAGE_SUPERADMIN_EMAIL = 'you@example.com'
   $env:SAGE_SUPERADMIN_PASSWORD = '<your own password, min 10 chars>'
   .\Setup-Local.ps1            # -Server 'host\instance' to override
   ```
   The password only exists in your shell session; only its hash is stored.
2. Store secrets for the API (never in files committed to git):
   ```powershell
   cd backend\src
   dotnet user-secrets set "DatabaseSettings:ConnectionString" "Server=localhost\SQLEXPRESS;Database=SageAdmin_Dev;Integrated Security=true;TrustServerCertificate=true"
   dotnet user-secrets set "JwtIssuerOptions:SecurityKey" "<random string, at least 32 chars>"
   ```
3. Run: `dotnet run --project backend/src` and `cd frontend; copy .env.example .env.local; npm run dev`.
   Check `GET /api/admin/status`.

Staging/Production: supply `DatabaseSettings__ConnectionString` and `JwtIssuerOptions__SecurityKey` as environment variables or from your secret store; set `ASPNETCORE_ENVIRONMENT`. Apply the schema with `Deploy-Database.ps1` (Production needs `-ConfirmProduction`).

## Tests

```powershell
dotnet test backend\SageAdmin.sln                       # unit + integration + database tests
dotnet test sage-common-lib\SageCommonLib.sln
cd frontend; npm run lint; npm run build
```
Integration tests create and drop their own database `SageAdmin_Test_<guid>` on `SAGE_TEST_SQL_SERVER` (default `localhost\SQLEXPRESS`) and refuse servers whose name contains "prod". They never touch `SageAdmin_Dev`.

## Database rules
- Schema is managed only by numbered scripts in `backend/database/DDL` (no EF migrations). Applied scripts are checksummed in `dbo.SchemaVersions`; never edit one after it is applied, add a new `ALTER` script.
- `Views/`, `Functions/`, `StoredProcedures/` use `CREATE OR ALTER` and re-run on every deploy; `SeedData/` is tracked like DDL.
- All SQL from application code is parameterized and lives in the data layer, never in controllers.
- Stored procedures (`StoredProcedures/`, one `dbo.usp_<Entity>_<Action>.sql` per file) hold anything that must be atomic or carries a business rule: multi-statement transactions, check-then-insert, lockout counting, permission-version bumps, the paged permission list, and the `/me` permission lookup. Simple single-row reads and inserts stay inline. Procedure names live in `ApplicationDatabaseConstants.StoredProcedures`; a test fails if one is missing from the database.
- After pulling new or changed procedures, re-run `Deploy-Database.ps1` (or `Setup-Local.ps1`) on your dev database.

## Backend layout (mirrors ixcheck-centre-planning-api)
`backend/src`: `Program.cs` (`--port=NNNNN`), `Startup.cs`, `Controllers/`, `AppFunctions/` (+`Interfaces/`; business logic and SQL via `IDatabaseFunctions`), `AppValidations/` (`ApplicationCommonFunctions`, `ApplicationValidationFunctions`), `Globals/` (`Application*Constants`, `ApplicationEnumeration`, `PermissionConstants`), `Models/` (`Filter/`, `HttpPost/`, `Interfaces/`), `Properties/`, `run.sh`. Dockerfiles (`backend/Dockerfile.{dev,qat,demo,prod}`) are built from the sage-admin root.

## Common library layout (mirrors ixcheck-common-lib)
`sage-common-lib/src`: `AppFunctions/` (+`Interfaces/`: `DatabaseFunctions`, `SqlConnectionFactory`, `LoggerFunctions`, `SerilogBootstrap`), `AppValidations/` (`CommonFunctions`, `Sanitization`, `StringExtensions`, `ValidationFunctions`), `Globals/` (`Constants`, `DatabaseConstants`, `Enumeration`, `JsonReturnConstants`), `Models/` (+`Interfaces/`, `Paging/`: responses, settings, `AppException` types), `Security/` (`SecurityHash`, `TokenGenerator`).

## Authentication (Sprint 2)
| Endpoint | Auth | Notes |
|----------|------|-------|
| `POST /api/admin/auth/login` | anonymous | `{username|email, password}` -> `{accessToken, expiresIn, user}`; sets HttpOnly `sage_rt` refresh cookie |
| `POST /api/admin/auth/refresh-token` | refresh cookie | rotates the refresh token; replaying an old one revokes the whole session family |
| `POST /api/admin/auth/logout` | refresh cookie | revokes that session, clears the cookie, idempotent |
| `POST /api/admin/auth/forgot-password` | anonymous | same answer for known/unknown email |
| `POST /api/admin/auth/reset-password` | reset token | single use, 30 min; revokes all sessions |
| `POST /api/admin/auth/change-password` | bearer | revokes all sessions |
| `GET /api/admin/auth/me` | bearer | user + permissions + modules from SQL |

- Access token: 15 min JWT with `sub`, `jti`, and `pv` (permission version) only. Every request also checks the user is still active and `pv` matches the database, so deactivation, password changes and permission changes apply immediately.
- Lockout: 5 consecutive failures lock the account for 15 minutes (`AuthSettings`). Login attempts go to `dbo.LoginHistories`; login, logout and password events go to `dbo.AuditLogs`.
- Auth endpoints are rate limited per IP (`RateLimiting:AuthPermitPerMinute`).
- Password reset email: no mail provider is configured yet. In Development the reset link is written to the API log; elsewhere nothing is sent (`IPasswordResetNotifier`).
- Production cookie settings: `AuthSettings:RefreshCookieSecure=true` (default) and `RefreshCookieSameSite` (`None` if the frontend is on another site).

## Permissions (Sprint 3)
- `GET /api/admin/permissions` (paged: `page`, `pageSize`, `search`, `moduleId`, `isActive`, `sortBy`, `sortDirection`), `GET /api/admin/permissions/modules?includeInactive=`, `POST /api/admin/permissions`, `PUT /api/admin/permissions/{id}`, `POST /api/admin/permissions/modules`, `PUT /api/admin/permissions/modules/{id}`.
- Every endpoint declares its permission with `[RequirePermission(PermissionConstants.X)]`. The backend checks it against SQL on every request: 401 without a valid session, 403 without the permission.
- A permission's code is `Module.Action`; module code, permission module and action never change after creation. System modules/permissions cannot be deactivated. Deactivating a module or permission bumps `PermissionVersion` of every affected user, so it takes effect on their next request.

## Permission groups (Sprint 4)
| Endpoint | Permission | Notes |
|----------|------------|-------|
| `GET /api/admin/groups` | `Group.View` | paged: `page`, `pageSize`, `search`, `status` (Active/Inactive), `sortBy` (name, code, status, userCount, createdAtUtc), `sortDirection` |
| `GET /api/admin/groups/{id}` | `Group.View` | group + permission matrix (every active permission, plus inactive ones the group still holds) |
| `POST /api/admin/groups` | `Group.Add` | `{code, name, description}`; 409 on duplicate code or name |
| `PUT /api/admin/groups/{id}` | `Group.Edit` | `{name, description, status}`; code never changes |
| `DELETE /api/admin/groups/{id}` | `Group.Delete` | 409 while any user is assigned |
| `PUT /api/admin/groups/{id}/permissions` | `Group.AssignPermissions` | `{permissionIds: [...]}` replaces the whole set; 400 for unknown ids or newly granted inactive permissions |

- `{id}` is the group's public GUID. System groups cannot be deactivated or deleted.
- Changing a group's permissions or status bumps `PermissionVersion` for its members, so their current access token stops working and the next session gets the new access. Unchanged saves do nothing (no bump, no audit row).
- Create, update, status change, delete and permission changes are audited; permission changes record the added and removed codes.
- UI: `/groups` lists groups; **Permissions** opens the matrix (modules by category × actions, row/column select-all). It is read-only without `Group.AssignPermissions`.

## Writing SQL (ixcheck pattern)
Build the statement with `StringBuilder _sqlQuery`, then call the shared wrappers: `ExecuteInsertAsync` (returns the new id), `ExecuteUpdateAsync` / `ExecuteDeleteAsync` (rows affected), `ExecuteCheckExistsAndInsertAsync` (409 if it exists). They return `(IFunctionReturn, long)` and never throw for duplicates, foreign-key conflicts or SQL errors. Unlike ixcheck, values are always `@parameters`, never concatenated into the SQL text.

Stored procedures: `ExecuteProcedureWriteAsync` (same `(IFunctionReturn, long)` shape; `long` = the procedure's `SELECT`ed value, by convention the new id or rows affected), `ExecuteProcedureScalarAsync`, `QueryProcedureAsync` (OUTPUT parameters are filled when it returns), `ExecuteProcedureAsync`. A procedure reports expected errors with `THROW 50400` (-> 400), `THROW 50404` (-> 404) or `THROW 50409` (-> 409), and the THROW message is returned to the client, so write it for the user. Other `THROW` numbers reach C# as `DatabaseException.SqlErrorNumber` (e.g. `50001` for an already-used refresh token).

## Frontend layout
`frontend/src/app/(admin)/<module>/page.tsx` is one folder per module (dashboard, users, groups, permissions, audit-logs, profile, settings, change-password); pages are thin and import their screens from `frontend/src/features/<module>/`. Shared UI is in `components/`, API calls in `services/`, types in `types/`.
