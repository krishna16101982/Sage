# Sage User Panel

The panel sales persons and sales managers sign in to. Their accounts are created in the Admin Panel (**Sales Users**).

| Folder | What | Stack |
|--------|------|-------|
| `backend/` | User Panel API (`src/`) and its tests (`Tests/`). Its own project, controllers and functions; shares only `../sage-admin/sage-common-lib` and the database. | ASP.NET Core, .NET 10 |
| `frontend/` | User Panel UI | Angular 21 |

Database scripts are **not** here: the schema is shared and lives in `../sage-admin/backend/database` (deploy with `Deploy-Database.ps1`).

Status: **Sprint 8 (foundation) done** — sign-in, session refresh, sign-out, change password, home page. Next: Sprint 9 (customers, sale page, my/team sales). Plan: [../SALES_DESIGN.md](../SALES_DESIGN.md).

## First-time setup
1. Apply the schema: `..\sage-admin\backend\database\Scripts\Deploy-Database.ps1` (see the Admin README).
2. Create `backend/src/appsettings.Local.json` (git-ignored) with **its own** signing key — never the Admin API's:
   ```json
   {
     "JwtIssuerOptions": { "SecurityKey": "<random string, at least 32 characters>" },
     "DatabaseSettings": { "ConnectionString": "Server=localhost\\SQLEXPRESS;Database=SageAdmin_Dev;Integrated Security=true;TrustServerCertificate=true" }
   }
   ```
3. Run the API: `dotnet run --project backend/src --launch-profile http` → http://localhost:5090 (`GET /api/user/status`).
4. Run the UI: `cd frontend; npm install; npm start` → http://localhost:4300 (forwards `/api` to :5090 via `proxy.conf.json`).

## API (`/api/user/...`)
| Endpoint | Auth | Notes |
|----------|------|-------|
| `POST /api/user/auth/login` | anonymous | `{username|email, password}` → `{accessToken, expiresIn, user}`; sets HttpOnly `sage_user_rt` cookie |
| `POST /api/user/auth/refresh-token` | refresh cookie | rotates the token; replaying an old one ends the whole session |
| `POST /api/user/auth/logout` | refresh cookie | ends this session; idempotent |
| `POST /api/user/auth/change-password` | bearer | ends every session; clears "must change password" |
| `GET /api/user/auth/me` | bearer | user, role (`SalesPerson`/`SalesManager`), `isManager`, `managerName` |
| `GET /api/user/status` | anonymous | API and database health |

- Separate identity from the Admin API: own issuer/audience (`sage.user`), own signing key, own cookie (`sage_user_rt`), own session tables (`SalesUserRefreshTokens`, `SalesUserLoginHistories`). Admin tokens are refused here and the reverse.
- Same rules as the Admin Panel: 15-minute access tokens, 5 failed sign-ins lock the account for 15 minutes, every request re-checks that the user is active and the token is current. An admin deactivating a sales user or resetting their password ends their User Panel sessions immediately.
- There is no self-service "forgot password": an admin resets it in the Admin Panel, and the user must change it at next sign-in.

## Tests
```powershell
dotnet test backend\SageUser.sln      # creates and drops its own SageAdmin_Test_<guid> database
cd frontend; npm run build
```
If a running API locks `bin/`, add `--artifacts-path backend\.artifacts`.
