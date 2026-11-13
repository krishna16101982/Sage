# Sage Admin — Angular frontend

An Angular 21 version of the Admin Panel. It talks to the same API (`../backend`) as the Next.js app in `../frontend`; both are kept.

## Run
```powershell
cd frontend-angular
npm install        # first time only
npm start          # http://localhost:4200
```
The API must be running on `http://localhost:5080` (`dotnet run --project backend/src`).

## Environments
The API address comes from `src/environments/`:

| File | Used by | `apiBaseUrl` |
|------|---------|--------------|
| `environment.ts` | `npm start` / `ng serve` (local) | `http://localhost:5080` (the API must allow `http://localhost:4200` in `Cors:AllowedOrigins`). Set it to `''` to go through `proxy.conf.json` instead (no CORS needed). |
| `environment.dev.ts` | `ng build --configuration=dev` | fill in the dev API URL |
| `environment.qat.ts` | `ng build --configuration=qat` | fill in the QAT API URL |
| `environment.prod.ts` | `ng build` (production) | fill in the production API URL |

`angular.json` swaps the file at build time (`fileReplacements`). The persistent build cache is switched off (`cli.cache.enabled: false`) because it failed on a machine low on memory; turn it back on with `npx ng cache enable`.

## Folder structure
Same layout as the ixcheck global app: one folder per feature under `modules/`, each with its own routes file and separate List / Add / Edit pages.

```
src/
  environments/                 environment.ts, .dev, .qat, .prod
  styles/                       theme.css (colours), components.css (named classes), layout.css
  app/
    app.routes.ts               top level: LoginLayout (sign-in pages) and PrimaryLayout (signed-in area)
    app.config.ts
    component/                  shared UI: alert, modal, pagination, sort-header, page-header,
                                coming-soon, reset-password-form
    directives/                 can.directive.ts  (*appCan="perm.X" hides UI the user may not use)
    services/
      rest/global-rest-service.ts   every API call (token in memory, silent refresh, error envelope)
      auth/                     auth.service.ts (signed-in user), auth.guard.ts (authGuard, guestGuard)
      permission/               permission.service.ts (has / hasAll), permission.guard.ts (route data.access_perms)
      message/                  message.service.ts (success notice carried from Add/Edit back to the list)
      admin-users/ sales-users/ groups/ permissions/ products/   one service per API area
    shared/
      interfaces/               API types (*.interface.ts)
      enumarations/             permission-codes.ts (Perm)
      utils/                    format.ts (INR, duration, date), password.ts
    modules/
      layout/                   primary-layout, primary-header, primary-navigation (sidebar built from the API's menu)
      login/                    login-layout, login-prompt, forgot-password, reset-password
      changepassword/
      dashboard/
      admin-users/              admin-users.routes.ts
        admin-users-list/       list page  + admin-users-filter/
        admin-users-add/        add page
        admin-users-edit/       edit page
        admin-users-reset-password/
      sales-users/              same shape (+ sales-users-form shared by add/edit)
      groups/                   list (+ filter, delete modal), add, edit, form, groups-permissions page
      permissions/              permissions list/add/edit/form, permission-modules list/add/edit/form, tabs
      products/                 products-list (catalog), product-categories add/edit/form,
                                products add/edit/form, product-plans add/edit/form
```

Every component is a `.ts` file (logic) with a matching `.html` file (markup) beside it. Pages use the class names from `src/styles/` in their `.html`; there are no per-component CSS files.

## URLs
Each feature has `/<feature>/list`, `/<feature>/add` and `/<feature>/:id/edit` (e.g. `/users/list`, `/users/add`, `/users/{id}/edit`, `/users/{id}/reset-password`). Products: `/products/list`, `/products/categories/add`, `/products/categories/:id/edit`, `/products/add?categoryId=`, `/products/:id/edit`, `/products/:productId/plans/add`, `/products/plans/:id/edit`. Permissions: `/permissions/list`, `/permissions/modules`, and their add/edit pages.

Route parameters and query parameters arrive as component inputs (`withComponentInputBinding`). Each route lists the permission it needs in `data.access_perms`, checked by `PermissionGuard`. Permissions in the UI are hints only: the API checks every request.

## Adding a screen
1. Add the API calls to a service in `services/<area>/`.
2. Create `modules/<feature>/<feature>-list|add|edit/` components (`.ts` + `.html`) and `<feature>.routes.ts`.
3. Load the routes from `app.routes.ts` (`loadChildren`).
4. Show it in the sidebar: on Permissions > Modules, give its module a **menu link** (the route, e.g. `/inventory`), a category and a sort order. Users see it when they have the module's `View` permission. No code change is needed for the menu.
