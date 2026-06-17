# SinDinero — Architecture

Last updated: 2026-06-17

## Overview

SinDinero is a personal money / budgeting web app built on .NET 10 using a
**Blazor Web App** with the **Auto** interactive render mode. It is a single
deployable unit: the server project hosts both the UI and the backend logic
(data access, auth), so there is **no separate backend API project**.

## Solution layout

```
sin-dinero/
  SinDinero.sln
  SinDinero/                 # Server host — the backend + server-rendered UI
    Components/              # Blazor components, layouts, routes
      Account/               # ASP.NET Identity auth pages (login, register, 2FA, passkeys)
      Layout/                # MainLayout, NavMenu, reconnect modal
      Pages/                 # Home, Weather (sample), Error, NotFound
    Data/
      ApplicationDbContext.cs  # EF Core context (Identity schema)
      ApplicationUser.cs       # User entity
      Migrations/              # EF Core migrations (CreateIdentitySchema)
    Program.cs               # App wiring: DI, auth, DB, render modes
  SinDinero.Client/          # WebAssembly project — components that run in the browser
    Pages/                   # Client-interactive pages (Counter, Auth)
    Program.cs               # WASM entrypoint
```

## How the two projects relate

- **SinDinero (server)** — runs on the server. Serves pages, talks to the
  database directly, owns authentication and secrets. This is the backend.
- **SinDinero.Client (WASM)** — components compiled to WebAssembly. With
  `Auto` render mode, a component is first rendered server-side (fast first
  paint over SignalR), then the browser downloads the WASM runtime and
  subsequent interactions run client-side. Components shared with the client
  must live here (or a shared lib) so they can compile to WASM.

Rule of thumb: anything touching the DB, secrets, or server-only services
stays in **SinDinero**. Pure interactive UI can live in **SinDinero.Client**.

## Data layer

- **EF Core + SQLite**. Local dev DB file `SinDinero/Data/app.db` (gitignored).
- Identity schema migration (`CreateIdentitySchema`) ships with the scaffold.
- Adding domain models: add `DbSet<>` to `ApplicationDbContext`, then
  `dotnet ef migrations add <Name>` and `dotnet ef database update`.
- SQLite chosen for zero-config local dev. Swap to PostgreSQL / SQL Server
  later by changing the provider in `Program.cs` and the connection string.

## Domain model

```
Category 1 ──< * Transaction
```

- **Category** — `Id, Name, Type (Expense|Income), MonthlyLimit?, UserId`. A
  bucket for classifying money. Unique per `(UserId, Name)`. `MonthlyLimit` is
  an optional fixed monthly spending cap (expense categories).
- **Transaction** — `Id, Amount (decimal 18,2, positive), Date, Note,
  CategoryId, UserId`. Income vs expense is inherited from the category's
  `Type`; the UI renders the +/- sign accordingly.
- **MonthlyCategorySummary** — `UserId, Year, Month, CategoryId, Total`.
  Precomputed aggregate (one row per user/month/category). The dashboard reads
  this instead of aggregating Transactions. Unique on
  `(UserId, Year, Month, CategoryId)`.
- Both transactional entities are owned by a user via a denormalized `UserId`
  string (the Identity user id). All queries filter by the current user.
- Pages: `/dashboard` (cards, doughnut, budget progress bars, 6-month trend
  bar, month picker, rebuild), `/transactions` (full CRUD + running totals),
  `/categories` (full CRUD incl. monthly limit). All `InteractiveServer`,
  `[Authorize]`. List queries use `AsNoTracking()` so inline edits on detached
  copies don't collide with the scoped DbContext.

### Summary consistency

All transaction writes go through `Services/TransactionService`:
- `AddAsync` / `DeleteAsync` mutate the transaction **and** apply a signed
  delta to its `MonthlyCategorySummary` row in the same `SaveChanges`. Zeroed
  rows are deleted.
- `UpdateAsync(tx, oldAmount, oldDate, oldCategoryId)` reverses the old delta
  then applies the new one (handles amount/date/category changes).
- `RebuildAsync(userId)` recomputes all summary rows from transactions
  (backfill / repair) — wired to the dashboard "Rebuild" button.

Do not add/remove transactions directly via the DbContext — always use the
service, or summaries drift.

### Charts

`/dashboard` uses Chart.js 4.4.3 (jsDelivr CDN, loaded in `App.razor`) via the
`wwwroot/js/charts.js` wrapper (`renderDoughnut` for expense-by-category,
`renderTrend` for the 6-month income/expense bars), invoked from Blazor with
`IJSRuntime` in `OnAfterRenderAsync`.

Adding a field/entity: edit/add the model in `Models/`, wire it in
`ApplicationDbContext`, then `dotnet ef migrations add <Name>` +
`dotnet ef database update`.

## Auth

ASP.NET Core Identity (`--auth Individual`). Full scaffolded flows: register,
login, email confirm, password reset, 2FA / authenticator, recovery codes,
passkeys, external logins, account management. User table created by the
initial migration.

## Run

```
dotnet run --project SinDinero
```

Migrations auto-apply on startup; the app opens at the login page.

## Build

```
dotnet build SinDinero.sln
```
