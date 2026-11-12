# Sage Sales — Design (draft for approval)

Adds a product catalog, sales users (sales person / sales manager) and sale recording.
Status: **draft, awaiting approval.** Nothing below is built yet.

## 1. Decisions (from you)
| # | Decision |
|---|----------|
| D1 | Sales users sign in to a separate **User Panel**: `sage-user/frontend` (Next.js) + `sage-user/backend` (ASP.NET Core). The User API is a **separate project with its own controllers and functions** (nothing shared with the admin backend except the folder layout); **only `sage-common-lib` is shared**. It uses the **same SQL database**; schema scripts stay in one place (`sage-admin/backend/database`). |
| D2 | Admins / Super Admins create and manage sales users in the **Admin Panel**. Sales users never see the Admin Panel. |
| D3 | A sales manager is also a sales person (can sell). Many sales persons report to one manager. |
| D4 | Categories and products are dynamic (admin adds more). Seed: **SaaS** (SHTMS, GreytHR, LinkedIn, Freshdesk) and **Cyber Security** (no products yet). |
| D5 | Each product has **fixed plans** (e.g. Monthly, Yearly, Business, Individual). Each plan has a list **price** and a **cost**, both from the backend. |
| D6 | On a sale the sales person fills **either** the sale price **or** the discount; the other is calculated. Price and cost are not editable on the sale. |
| D7 | Renewal date = start date + duration (Month / Quarter / Year), editable. |

## 2. Assumptions (correct any that are wrong)
| # | Assumption |
|---|------------|
| A1 | Totals: list amount = plan price × quantity; cost amount = plan cost × quantity; **discount = list amount − sale price** (amount; the % is shown alongside). |
| A2 | Sale price cannot exceed the list amount (discount ≥ 0) and cannot be negative. |
| A3 | Duration = count + unit (e.g. 1 Year, 3 Months). A plan has a default duration; the sale can change it. |
| A4 | Price and cost are **copied onto the sale** when it is saved, so later price-list changes never alter past sales. Price changes are audited. |
| A5 | Currency is **INR** for everything (no multi-currency). |
| A6 | Sales users can see the cost and margin on the sale page. *(Say if cost must be hidden from sales persons.)* |
| A7 | Customers are **shared**: every sales user can search all customers and add new ones (avoids duplicates). Customer fields: company/name, contact person, email, phone, GSTIN, address, city, state, country. |
| A8 | Visibility: a sales person sees **their own** sales; a manager sees **own + their team's**; Admin Panel sees all (reports later). |
| A9 | One reporting level: sales person → manager. A manager has no manager. Changing a person's manager does not move their past sales. |
| A10 | A sale can be **cancelled** (not deleted). Editing a saved sale is out of scope for the first version. |
| A11 | Sales users are a **separate table** from admin users (separate login, tokens, lockout, history), using the same security rules as the Admin Panel. |

## 3. Data model (new tables, all in the same database)
| Table | Key columns |
|-------|-------------|
| `ProductCategories` | Code (UQ), Name (UQ), Description, SortOrder, IsActive |
| `Products` | CategoryId FK, Code (UQ), Name, Description, SortOrder, IsActive; UQ(CategoryId, Name) |
| `ProductPlans` | ProductId FK, Name, DurationUnit (Month/Quarter/Year), DurationCount, Price DECIMAL(18,2), Cost DECIMAL(18,2), SortOrder, IsActive; UQ(ProductId, Name); CK price/cost ≥ 0 |
| `SalesUsers` | PublicId, FirstName, LastName, Username (UQ), Email (UQ), Mobile, PasswordHash, MustChangePassword, Role (SalesPerson/SalesManager), ManagerId FK → SalesUsers, Status, FailedLoginCount, LockoutEndUtc, PermissionVersion, LastLoginAtUtc, IsDeleted, CreatedBy/UpdatedBy (admin user) |
| `SalesUserRefreshTokens`, `SalesUserPasswordResetTokens`, `SalesUserLoginHistories` | same shape as the admin equivalents |
| `Customers` | PublicId, Name, ContactPerson, Email, Phone, GSTIN, Address, City, State, Country, IsActive, CreatedBySalesUserId |
| `Sales` | PublicId, SaleNumber (UQ, e.g. `S-2026-000123`), CustomerId, CategoryId, ProductId, PlanId, SalesUserId, ManagerId (snapshot), Quantity, UnitPrice, UnitCost, ListAmount, SaleAmount, DiscountAmount, CostAmount, DurationUnit, DurationCount, StartDate, RenewalDate, Status (Active/Cancelled), Notes, CreatedAtUtc |

Writes with rules (sale creation and totals, manager assignment, plan price change) go through stored procedures, as in the Admin API. The sale procedure **recalculates totals from the database price list** and rejects a sale price above the list amount, so the browser cannot change price or cost.

## 4. Sale page (User Panel)
1. Category → Product → Plan (only active ones). Plan fills **Price** and **Cost** (read-only) and the default **Duration**.
2. Customer: search existing, or **Add new customer** in a dialog.
3. Quantity → List amount = price × qty.
4. Enter **Sale price** → discount (amount and %) calculated; or enter **Discount** → sale price calculated.
5. Start date (default today) + duration → **Renewal date** calculated (editable).
6. Save → sale number shown. "My sales" list (manager: "Team sales" too).

## 5. Proposed sprints
| Sprint | Panel | Scope |
|--------|-------|-------|
| 5 | Admin | Product catalog: categories, products, plans with price and cost; seed SaaS + Cyber Security; permissions `Product.View/Add/Edit` (one module for categories, products and plans); audit of price changes |
| 6 | Admin | Sales users: create/edit, role, manager, activate/deactivate, reset password; permissions `SalesUser.*` |
| 7 | User | `sage-user` foundation: backend (shared common lib, same DB), sales-user login/refresh/logout/change password, frontend shell |
| 8 | User | Customers + sale page + My sales / Team sales |
| later | Admin | Admin Users (was Sprint 5), Dashboard & Audit log screens, Settings, sales reports |

Housekeeping with Sprint 7: rename `sage-user/bakend` → `sage-user/backend`.
