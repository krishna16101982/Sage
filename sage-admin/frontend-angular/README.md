# Sage Admin — Angular frontend

An Angular 21 version of the Admin Panel. It talks to the same API (`../backend`) as the Next.js app in `../frontend`; both are kept.

## Run
```powershell
cd frontend-angular
npm install        # first time only
npm start          # http://localhost:4200
```
The API must be running on `http://localhost:5080` (`dotnet run --project backend/src`). In development, `ng serve` forwards every `/api/...` call to it (`proxy.conf.json`), so the browser never makes a cross-site call and no CORS setup is needed.

Build: `npm run build` (output in `dist/`). For a deployment where the API is on another host, set `API_BASE_URL` in `src/app/core/config.ts` and add the site to the API's `Cors:AllowedOrigins`.

The persistent build cache is switched off (`angular.json` → `cli.cache.enabled: false`) because it failed on a machine low on memory. Turn it back on with `npx ng cache enable` for faster rebuilds.

## Layout
| Folder | What |
|--------|------|
| `src/app/core/` | `api-client.ts` (API calls, token in memory, silent refresh), `auth.service.ts` (signed-in user), `guards.ts`, `can.directive.ts` (`*appCan` hides UI by permission), `admin-api.ts` (one service per screen), `models/` (API types) |
| `src/app/layout/` | `shell.component.ts`: sidebar + header around the signed-in pages |
| `src/app/shared/` | `ui.ts` (alert, modal, pagination, sortable header), `reset-password.dialog.ts` |
| `src/app/pages/` | one folder per screen: `auth/`, `users/`, `sales-users/`, `groups/`, `permissions/`, `products/` |
| `src/styles/` | `theme.css` (colours), `components.css` (named classes) — same files as the Next.js app; `layout.css` (reset + small layout helpers) |

Every page, dialog and shared component is a `.ts` file (logic) with a matching `.html` file (markup) beside it, e.g. `pages/users/admin-users.page.ts` + `admin-users.page.html`. Files that hold several dialogs have one `.html` per dialog (e.g. `group-dialogs.ts` → `group-form.dialog.html`, `delete-group.dialog.html`). Pages use the class names from `src/styles/` in their `.html`; there are no per-page CSS files.

Routes are in `src/app/app.routes.ts`. Permissions are UI hints only: the API checks every request.
