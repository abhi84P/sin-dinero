# Decision Log

Append-only record of what was decided and why. Newest first.

---

## 2026-06-17 — Transaction edit, budgets, trend chart

### D16: Multi-month trend chart (last 6 months)
**What:** Dashboard renders a grouped bar chart (income vs expense) over the
6-month window ending at the selected month, via `renderTrend` in charts.js.
**Why:** Requested. Reads from `MonthlySummaries` (one windowed query),
aggregates the 6 buckets in memory — cheap, no extra table.

### D15: Budget limits = optional MonthlyLimit on Category
**What:** Added nullable `Category.MonthlyLimit` (decimal). Dashboard shows a
progress bar per expense category with a limit: spent-this-month vs limit,
red/over highlight.
**Why:** Simplest model — a fixed monthly cap per category. Null = no limit.
**Rejected:** a separate per-month budget table (over-engineered for now).
**Revisit when:** users need different limits per month or per period — then
promote to a `Budget(UserId, CategoryId, Year, Month, Amount)` table.

### D14: Transaction edit via TransactionService.UpdateAsync
**What:** `UpdateAsync(tx, oldAmount, oldDate, oldCategoryId)` reverses the old
summary delta then applies the new one, in one `SaveChanges`. Transactions page
got an inline edit (Edit button → form → Save/Cancel).
**Why:** Completes D10 — edits must keep `MonthlyCategorySummary` correct even
when amount, date (→ month), or category changes.
**Notes:** List queries switched to `AsNoTracking()` (Categories + Transactions
+ Dashboard) so editing a detached copy doesn't collide with tracked entities
in the scoped DbContext.

## 2026-06-17 — Precomputed summaries + dashboard

### D13: Category CRUD stays user-defined (no fixed enum)
**What:** Categories remain rows users create/edit/delete, not a hard-coded
enum. Added Edit to complete CRUD (was create/read/delete).
**Why:** User confirmed CRUD is acceptable over a fixed enum list. User-defined
categories are more flexible for a personal money app.

### D12: Dashboard chart = Chart.js via CDN + JS interop
**What:** `/dashboard` renders a doughnut (expense by category) using Chart.js
4.4.3 from jsDelivr CDN, wrapped by `wwwroot/js/charts.js`, called from Blazor
via `IJSRuntime` in `OnAfterRenderAsync`.
**Why:** No NuGet/build dependency; one small JS wrapper. Real interactive
charts vs. hand-rolled CSS bars.
**Cost / revisit:** CDN dependency (needs internet; not offline). Move to a
self-hosted/bundled Chart.js or a Blazor-native chart lib if offline or CSP
hardening is needed.

### D11: Store precomputed monthly breakdown (MonthlyCategorySummary)
**What:** New table: one row per `(UserId, Year, Month, CategoryId)` holding
the summed `Total`. Dashboard reads it directly instead of GROUP BY over
Transactions.
**Why:** User asked to persist the monthly-by-category breakdown to avoid
recomputing on every fetch.
**Consistency:** Maintained incrementally by `TransactionService` (see D10);
empty rows (Total == 0) are deleted. `RebuildAsync` recomputes from scratch
for backfill/repair (exposed as the dashboard "Rebuild" button).
**Tradeoff:** Denormalized aggregate must be kept in sync — all transaction
writes MUST go through the service. Alternative (on-the-fly GROUP BY) was
simpler but rejected per the requirement. Category *type* is NOT duplicated
into the summary — it's read by joining Category, so editing a category's type
needs no rebuild.

### D10: All Transaction writes go through TransactionService
**What:** `Services/TransactionService` owns `AddAsync` / `DeleteAsync` /
`RebuildAsync`. Pages no longer add/remove transactions directly. Registered
scoped.
**Why:** Single choke point so the summary delta (D11) is always applied in the
same `SaveChanges`. Prevents drift between Transactions and summaries.
**Revisit:** add an `UpdateAsync` (reverse old delta + apply new) once
transaction editing exists.

## 2026-06-17 — Domain model v1 + sample cleanup

### D9: Removed Counter + Weather samples
**What:** Deleted `Counter.razor` and `Weather.razor` and their nav links.
**Why:** Template demo pages, not part of the product. Nav now points at the
real features (Transactions, Categories).

### D8: Domain model = Category + Transaction
**What:** Two entities. `Category` (Name, Type, UserId). `Transaction`
(Amount, Date, Note, CategoryId, UserId). `TransactionType` enum
(Expense / Income) lives on the category.
**Why:** Income/expense is a property of the bucket, not each entry, so a
transaction inherits its sign from its category — fewer fields, no ambiguity.
Amount always stored positive; the UI applies +/- from the category type.
**Notes:**
- `Amount` mapped `decimal(18,2)` — SQLite has no native decimal type.
- Unique index on `(UserId, Name)` for categories; index on
  `(UserId, Date)` for transactions (per-user date queries).
- Category delete uses `DeleteBehavior.Restrict` — can't delete a category
  that still has transactions (UI checks first).

### D7: Per-user data via denormalized UserId
**What:** Both entities carry `UserId` (the Identity user id string). Every
query filters by the current user's id (from the auth state claim).
**Why:** Simple, explicit row-level ownership without extra join tables. The
denormalized `UserId` on `Transaction` avoids a join-through-Category just to
scope a user's list.
**Revisit when:** adding shared/household budgets — then ownership moves to a
group entity.

### D6: Pages inject scoped DbContext directly
**What:** `Transactions.razor` / `Categories.razor` are `InteractiveServer`,
`[Authorize]`, and inject `ApplicationDbContext` directly.
**Why:** Fastest path for a starter; one scoped context per circuit.
**Cost / revisit:** A single scoped context is not safe for *concurrent*
operations on one circuit. If we add parallel async DB calls, switch to
`IDbContextFactory<ApplicationDbContext>` (create a short-lived context per
operation). Fine as-is while calls are sequential.

## 2026-06-17 — Initial scaffold

### D1: Frontend = Blazor Web App, Auto render mode
**What:** Scaffolded with `dotnet new blazor --interactivity Auto`.
**Why:** Auto gives a fast server-rendered first paint, then transparently
switches interactions to WebAssembly in the browser. Best of both server and
client models without committing to one. Modern .NET default.
**Alternatives rejected:**
- *Blazor Server only* — simplest, but every interaction needs a live SignalR
  connection; weaker offline / latency story.
- *WASM standalone* — would force a separate backend API (see D2).

### D2: No separate backend project
**What:** Single server project (`SinDinero`) hosts UI + data + auth.
**Why:** With server-side render modes the server project already has direct,
secure DB access. A separate Web API would add a network hop, DTO mapping, and
deployment surface for zero current benefit.
**Revisit when:** we need a public API or a native mobile client — then add a
`SinDinero.Api` project and a `SinDinero.Shared` DTO library.

### D3: Data = EF Core + SQLite
**What:** EF Core with SQLite file DB, wired via `--auth Individual`.
**Why:** Zero-config local dev — no DB server to install. EF Core keeps the
provider swappable. Money app needs persistence and per-user data from day one.
**Revisit when:** going to production / multi-user hosting — migrate provider
to PostgreSQL or SQL Server (change provider + connection string, regenerate
migrations).

### D4: Auth = ASP.NET Core Identity (Individual)
**What:** `--auth Individual` scaffolds full Identity (login, register, 2FA,
passkeys, account management).
**Why:** A money app is inherently per-user; accounts are not optional. Using
the built-in template also auto-wires the EF Core + SQLite plumbing we wanted
in D3, so we get the data layer "for free."
**Cost:** ~40 scaffolded auth pages now in the repo. Acceptable — they are
standard and editable.

### D5: SQLite DB files gitignored
**What:** Added `*.db`, `*.db-shm`, `*.db-wal` to `.gitignore`; deleted the
stray `app.db` the template generated.
**Why:** Local dev databases are environment state, not source. Each dev /
environment regenerates from migrations.
