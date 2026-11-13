# Sage User Panel

The panel sales persons and sales managers sign in to. Their accounts are created in the Admin Panel (**Sales Users**).

| Folder | What | Stack |
|--------|------|-------|
| `backend/` | User Panel API (`src/`) and its tests (`Tests/`). Its own project, controllers and functions; shares only `../sage-admin/sage-common-lib` and the database. | ASP.NET Core, .NET 10 |
| `frontend/` | User Panel UI, same folder structure as the Admin Angular app (see below). Each component is a `.ts` (logic) with a matching `.html` (markup); styles in `src/styles/` (same as the Admin Panel). | Angular 21 |

Database scripts are **not** here: the schema is shared and lives in `../sage-admin/backend/database` (deploy with `Deploy-Database.ps1`).

Status: **Sprint 8 (foundation) and Sprint 9 (sales) done** — sign-in, session refresh, sign-out, change password; customers; the sale page; My sales, Team sales (managers), sale details and cancelling. Plan: [../SALES_DESIGN.md](../SALES_DESIGN.md).

## Screens
| Screen | URL | Who |
|--------|-----|-----|
| Home (this month's sales, team figures for a manager) | `/home` | everyone |
| New sale | `/sales/add` | everyone |
| My sales | `/sales/list` | everyone |
| Team sales (filter by sales person) | `/sales/team` | sales managers |
| Sale details, Cancel sale (with a reason) | `/sales/{id}` | the seller and their manager |
| Customers (shared by the whole team), Add customer | `/customers/list`, `/customers/add` | everyone |

**Sale page:** Category > Product > Plan fills the price, cost and duration from the price list (read-only). Pick a customer by searching, or add one in a dialog. Enter **either** the sale price **or** the discount (amount or %): the other is calculated, with list amount, cost and margin shown. Renewal date = start date + duration, editable. When the sale is saved, the database recalculates every amount from the price list, so the browser cannot change price or cost.

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

## Frontend structure (`frontend/src/`)
```
environments/            environment.ts (local: apiBaseUrl '' → proxy to :5090), .dev, .qat, .prod
app/
  app.routes.ts          LoginLayout (sign-in) and PrimaryLayout (signed-in area)
  component/             alert, modal, pagination, sort-header, page-header
  services/rest/         global-rest-service.ts (API calls, token in memory, silent refresh)
  services/auth/         auth.service.ts, auth.guard.ts (authGuard, guestGuard, managerGuard)
  services/              catalog/, customers/, sales/ (one service per API area), message/ (notice after saving)
  shared/interfaces/     API types;  shared/utils/  format.ts, dates.ts (renewal date), password.ts
  modules/
    layout/              primary-layout, primary-header, primary-navigation
    login/               login-layout, login-prompt
    changepassword/
    home/
    customers/           customers.routes.ts, customers-list (+ customers-filter), customers-add, customers-form
    sales/               sales.routes.ts
      sales-add/         the sale page (+ customer-picker: search, or "Add new customer" dialog)
      sales-list/        My sales and Team sales (+ sales-filter); the route data says which
      sales-view/        one sale (+ sales-cancel-modal)
```
New screens go in `modules/<feature>/` with their own `<feature>.routes.ts` and separate list / add / edit pages, like the Admin app. `ng build --configuration=dev|qat|production` picks the matching environment file.

## API (`/api/user/...`)
| Endpoint | Auth | Notes |
|----------|------|-------|
| `POST /api/user/auth/login` | anonymous | `{username|email, password}` → `{accessToken, expiresIn, user}`; sets HttpOnly `sage_user_rt` cookie |
| `POST /api/user/auth/refresh-token` | refresh cookie | rotates the token; replaying an old one ends the whole session |
| `POST /api/user/auth/logout` | refresh cookie | ends this session; idempotent |
| `POST /api/user/auth/change-password` | bearer | ends every session; clears "must change password" |
| `GET /api/user/auth/me` | bearer | user, role (`SalesPerson`/`SalesManager`), `isManager`, `managerName` |
| `GET /api/user/status` | anonymous | API and database health |
| `GET /api/user/catalog` | bearer | active categories > products > plans (price, cost, duration); products without an active plan are left out |
| `GET /api/user/customers` | bearer | `search`, `page`, `pageSize`, `sortBy` (name/city/createdAtUtc); each with its active sale count |
| `GET /api/user/customers/{id}` · `POST /api/user/customers` | bearer | add: name required; email, phone, GSTIN checked; 409 for the same GSTIN or the same name in the same city |
| `POST /api/user/sales` | bearer | `{customerId, planId, quantity, saleAmount | discountAmount, durationUnit?, durationCount?, startDate, renewalDate?, notes?}` → the sale with its number (`S-2026-000123`) and totals |
| `GET /api/user/sales` | bearer | `scope=mine|team` (team: managers only, else 403), `sellerId`, `search`, `status`, `soldFrom`, `soldTo`, sorting, paging; plus `totalSaleAmount` and `totalMargin` of the matching active sales |
| `GET /api/user/sales/{id}` | bearer | own sale, or a team member's for their manager (404 otherwise) |
| `POST /api/user/sales/{id}/cancel` | bearer | `{reason}`; the seller or their manager; a cancelled sale stays on record |
| `GET /api/user/sales/team-members` | bearer | the manager's team (403 for a sales person) |

**Sales rules** (stored procedures `usp_Sale_Create`, `usp_Sale_Cancel`, `usp_Sale_GetPaged`, tables `Customers`, `Sales`):
- List amount = plan price × quantity; cost = plan cost × quantity; discount = list amount − sale price. The sale price must be between 0 and the list amount. Price and cost are copied onto the sale, so later price-list changes never alter past sales.
- Only an active plan of an active product in an active category can be sold, to an active customer. The start date must be within a year of today.
- Team sales are the sales of the people who reported to the manager **when the sale was made**: changing a person's manager later does not move their past sales.
- Customer added, sale recorded and sale cancelled are written to `AuditLogs` (the sales user is in `NewValues`).

- Separate identity from the Admin API: own issuer/audience (`sage.user`), own signing key, own cookie (`sage_user_rt`), own session tables (`SalesUserRefreshTokens`, `SalesUserLoginHistories`). Admin tokens are refused here and the reverse.
- Same rules as the Admin Panel: 15-minute access tokens, 5 failed sign-ins lock the account for 15 minutes, every request re-checks that the user is active and the token is current. An admin deactivating a sales user or resetting their password ends their User Panel sessions immediately.
- There is no self-service "forgot password": an admin resets it in the Admin Panel, and the user must change it at next sign-in.

## Tests
```powershell
dotnet test backend\SageUser.sln      # creates and drops its own SageAdmin_Test_<guid> database
cd frontend; npm run build
```
If a running API locks `bin/`, add `--artifacts-path backend\.artifacts`.
