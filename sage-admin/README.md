# Sage Admin

Three application folders (do not add more without approval):

| Folder | What | Stack |
|--------|------|-------|
| `sage-common-lib/` | Shared library (responses, exceptions, DB access, security, validation, logging) reusable by the future User Panel | .NET 10 class library |
| `backend/` | Admin API (`src/`), SQL scripts (`database/`), tests (`Tests/`) | ASP.NET Core, .NET 10, SQL Server |
| `frontend/` | Admin Panel UI | Next.js (App Router), React, TypeScript, Tailwind |
| `frontend-angular/` | Admin Panel UI, Angular version (same API and screens; both kept) — see its README | Angular 21, TypeScript |

Design and plan: [SAGE_ANALYSIS.md](SAGE_ANALYSIS.md). Current status: **Sprint 7 (Admin users) implemented. Next: Sprint 8 (User Panel foundation in `../sage-user`).** Sales plan: [../SALES_DESIGN.md](../SALES_DESIGN.md).

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

## Product catalog (Sprint 5)
Categories → products → plans. A plan is a price-list entry: name, duration (count × Month/Quarter/Year), **price** and **cost** per unit in INR. The User Panel will sell from this catalog.

| Endpoint | Permission | Notes |
|----------|------------|-------|
| `GET /api/admin/products/catalog?includeInactive=` | `Product.View` | full tree; without `includeInactive` only what can be sold |
| `POST /api/admin/products/categories` · `PUT .../categories/{id}` | `Product.Add` · `Product.Edit` | code fixed after create; name unique |
| `POST /api/admin/products` · `PUT .../{id}` | `Product.Add` · `Product.Edit` | code unique, name unique per category; may move to another active category |
| `POST /api/admin/products/{productId}/plans` · `PUT .../plans/{id}` | `Product.Add` · `Product.Edit` | price/cost required, ≥ 0, max 2 decimals; duration 1–120 |

- Nothing is deleted: categories, products and plans are deactivated. Nothing can be added under an inactive parent.
- Price and cost changes are audited (old and new values) and affect future sales only.
- Seed (`SeedData/002`): categories **SaaS** (SHTMS, GreytHR, LinkedIn, Freshdesk) and **Cyber Security**; plans and prices are entered in the UI.
- UI: `/products` (sidebar **Products**).

## Admin users (Sprint 7)
| Endpoint | Permission | Notes |
|----------|------------|-------|
| `GET /api/admin/users` | `AdminUser.View` | paged: `search`, `status`, `groupId`, `sortBy`, `sortDirection` |
| `GET /api/admin/users/groups` · `GET .../{id}` | `AdminUser.View` | active groups for the drop-down (no `Group.View` needed); one user |
| `POST /api/admin/users` | `AdminUser.Add` | details + optional group + temporary password; must change it at first sign-in |
| `PUT /api/admin/users/{id}` | `AdminUser.Edit` | details and group (one group per user; none = no permissions); username fixed |
| `PUT /api/admin/users/{id}/status` | `AdminUser.ChangeStatus` | `Active` (also unlocks) or `Inactive` (ends their sessions) |
| `POST /api/admin/users/{id}/reset-password` | `AdminUser.ResetPassword` | temporary password; ends their sessions |

- Super Admins are not created here (the deploy script creates the first one). Only a Super Admin can edit, deactivate or reset a Super Admin (403 otherwise). Nobody can deactivate or reset themselves (use Change password).
- A group change, deactivation or reset bumps `PermissionVersion`, so the user's current access token stops working on the next request. All changes are audited without password data.
- UI: `/users` (sidebar **Admin Users**).

## Sales users (Sprint 6)
User Panel accounts, kept in `dbo.SalesUsers` (separate from admin users). Roles: **SalesPerson** (must report to an active SalesManager) and **SalesManager** (can also sell; has no manager). Created and managed here; they sign in only to the User Panel.

| Endpoint | Permission | Notes |
|----------|------------|-------|
| `GET /api/admin/sales-users` | `SalesUser.View` | paged: `search`, `role`, `status`, `managerId` (a manager's team), `sortBy`, `sortDirection` |
| `GET /api/admin/sales-users/managers` · `GET .../{id}` | `SalesUser.View` | active managers for the drop-down; one user |
| `POST /api/admin/sales-users` | `SalesUser.Add` | details + role + manager + temporary password; the user must change it at first sign-in |
| `PUT /api/admin/sales-users/{id}` | `SalesUser.Edit` | details, role, manager; username fixed; a manager with a team cannot become a sales person (409) |
| `PUT /api/admin/sales-users/{id}/status` | `SalesUser.ChangeStatus` | `Active` (also clears a lockout) or `Inactive` |
| `POST /api/admin/sales-users/{id}/reset-password` | `SalesUser.ResetPassword` | temporary password, must change at next sign-in |

- Username and email are unique among non-deleted sales users. Role, manager, status and password changes bump `PermissionVersion`, so User Panel sessions pick them up on the next request (enforced once the User Panel exists).
- Create, edit, status and password resets are audited; password data never appears in responses or audit rows.
- UI: `/sales-users` (sidebar **Sales Users**).

If a running API locks `backend/src/bin`, build or test side by side with `dotnet test backend\SageAdmin.sln --artifacts-path backend\.artifacts` (ignored by git).

## Writing SQL (ixcheck pattern)
Build the statement with `StringBuilder _sqlQuery`, then call the shared wrappers: `ExecuteInsertAsync` (returns the new id), `ExecuteUpdateAsync` / `ExecuteDeleteAsync` (rows affected), `ExecuteCheckExistsAndInsertAsync` (409 if it exists). They return `(IFunctionReturn, long)` and never throw for duplicates, foreign-key conflicts or SQL errors. Unlike ixcheck, values are always `@parameters`, never concatenated into the SQL text.

Stored procedures: `ExecuteProcedureWriteAsync` (same `(IFunctionReturn, long)` shape; `long` = the procedure's `SELECT`ed value, by convention the new id or rows affected), `ExecuteProcedureScalarAsync`, `QueryProcedureAsync` (OUTPUT parameters are filled when it returns), `ExecuteProcedureAsync`. A procedure reports expected errors with `THROW 50400` (-> 400), `THROW 50404` (-> 404) or `THROW 50409` (-> 409), and the THROW message is returned to the client, so write it for the user. Other `THROW` numbers reach C# as `DatabaseException.SqlErrorNumber` (e.g. `50001` for an already-used refresh token).

## Frontend styling (how to change the look)
All styling lives in three files under `frontend/src/styles/`:

| File | Change it to... |
|------|-----------------|
| `theme.css` | change colours, corner radius, fonts, sidebar width. Every colour is a named variable (`--color-primary`, `--color-danger`, ...) with a light value and a dark-mode value. |
| `components.css` | change how a kind of element looks everywhere: buttons (`btn btn-primary`, `btn-secondary`, `btn-danger`, `link-button`), form fields (`field`, `field-label`, `checkbox-label`), tables (`table-wrap`, `data-table`, `amount`, `actions`, `date`), `badge`, `alert`, `card`, `tabs`, dialogs (`modal`), sidebar and header. |
| `globals.css` | entry point only (imports the two files above and Tailwind). |

Screens use these class names, plus a few plain Tailwind utilities for layout (`flex`, `gap-2`, `w-64`, `space-y-4`). Example: to make every main button blue, set `--color-primary` (and `--color-primary-hover`) in `theme.css`; nothing else changes.

## Frontend layout
`frontend/src/app/(admin)/<module>/page.tsx` is one folder per module (dashboard, users, groups, permissions, audit-logs, profile, settings, change-password); pages are thin and import their screens from `frontend/src/features/<module>/`. Shared UI is in `components/`, API calls in `services/`, types in `types/`.
