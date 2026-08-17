# Engineering Memory

Keep this file factual, concise, and free of credentials.

## 2026-08-07 — Priority Desk 15-problem fix

### Task completed

Identified and fixed 15 problems in `priority-desk.html`. Backed up original to `backups/priority-desk-backup-20260807/`.

### Fixes applied

1. `toggleSelectAll` — now selects from filtered list, not raw `allOrders[0]`
2. `selectAll` checkbox — properly toggles selection state
3. Loading spinner — shows while API data loads (CSS spinner animation)
4. Session validation — `validateSession()` calls `/api.ashx?action=session` on restore from sessionStorage
5. Auto-refresh paused — interval skips refresh while action panel is open
6. Escape key — closes action panel when open
7. Enter key — saves priority from action panel
8. API error messages — `loadOrders` catch now shows `e.message` to user
9. Order count indicator — shows "X of Y orders" in desk view toolbar
10. Report date defaults — auto-fills today's date when switching to report view
11. Report summary — adds "No Date" and "No Remarks" count cards
12. Report remarks tooltip — adds `title` attribute to remarks cell in report table
13. Mobile responsive — action panel details grid stacks to single column, toolbar stacks vertically
14. savePriority validation — warns if priority is High but packing date is empty
15. Logout cleanup — clears `refreshTimer`, `filteredOrders`, removes keydown listener

### Files modified

- `priority-desk.html` — all 15 fixes
- `backups/priority-desk-backup-20260807/priority-desk.html` — backup (21KB)

### Verification

- JS syntax validated with `node --check`
- Deployed to Site 1, returns HTTP 200 (23,619 bytes)

## 2026-08-07 — Packing portal fixes and history improvements

### Task completed

Fixed multiple packing portal issues for user `pk` (Machine User, station "Packed"). Server-side station checks, history API, portal dropdown filtering, and Machine User history visibility were all corrected.

### Files modified

- `PmsApiHandler.cs` — `packing-boxes-set` and `production-balance-save` now allow both "Packing" and "Packed" stations; `EnsurePackingQueueEntryForPortal` falls back through Packing → Packed → user's station; `BuildHistoryStandaloneState` joins tbl_dealers for customer_name, includes confirmation_date, packed_boxes (from tbl_dispatch_boxes), balance_boxes (from tbl_orders); `IsHistoryVisibleToUser` changed to `return true` for Machine User (station filter was too restrictive)
- `packing-portal.html` — Planning rows filtered by prodLookup to exclude fully packed orders (box_count > 0 AND balance = 0); history table headers updated to Order no / Customer Name / Confirmation Date / Pack Qty / Balance Qty; station_name filter removed from history rows

### Key decisions

- `packing_balance_box_qty` read from `tbl_orders` (not `tbl_production_planner`) via main query
- Machine User history visibility changed to `return true` because `station_name` from `VisibleStationNames` uses "-" as placeholder, failing station-match logic
- Planning rows do not carry `box_count` or `packing_balance_box_qty` — used production rows lookup (prodLookup) to filter fully packed
- History grouped to one latest row per order, preserving API newest-first order

### Verification

- Local `PmsApiHandler.cs` compiles cleanly (no `balanceLookup` references remain)
- **Note:** Live API currently returns 500 for `history-state` and `app-state` — likely the deployed version has stale code; requires redeploy via `deploy-qr-scanner.ps1` or manual FTP upload of PmsApiHandler.cs

## 2026-08-07 — Priority Desk deployed

### Task completed

Built and deployed Priority Desk page to Site 1. Added `priority_date` column to `tbl_production_planner` via `EnsureSchema` ALTER TABLE. Added `priority-desk-state` and `priority-report` API actions. Modified `HandlePlannerSave` to accept and persist `priority_date`. Updated login page Quick Access with Priority Desk link.

### Files modified

- `PmsApiHandler.cs` — Added `priority_date` schema, planner-save support, new API actions
- `priority-desk.html` — New standalone page for priority assignment
- `index.html` — Added Priority Desk Quick Access link
- `styles.css` — Updated quick link styles
- `PROJECT-CONTEXT.md` — Added Priority Desk entry
- `docs/phases.md` — Updated future roadmap
- `docs/memory.md` — This entry

### Key decisions

- Only "High" priority (no Medium/Low) per user requirement
- Uses existing `tbl_production_planner` table with new `priority_date` column
- Manual packing date pick (not auto-populated)
- Auto-removal when packed (workflow_stage_code=DISPATCH_READY)
- Fixed API typo: `BuildStatusLookup` → `LoadStatusLookup` in both new handlers

### Verification

- API returns 200 for `priority-desk-state` and `priority-report`
- Priority Desk page loads at `http://[removed]-site1.ktempurl.com/priority-desk.html`
- Login page has Priority Desk Quick Access link
- Test login: `admin.user` / `1` or `planner.user` / `1`

## 2026-07-23 — Project Blueprint established

### Task completed

Applied the `project-blueprint` documentation-first workflow to the Elenza PMS production-support workspace.

### Files added

- `docs/project.md`
- `docs/architecture.md`
- `docs/rules.md`
- `docs/design.md`
- `docs/phases.md`
- `docs/memory.md`

### Analysis

- Confirmed the product is a minimal modular-interiors production system.
- Confirmed the stack is plain HTML/CSS/JavaScript, ASP.NET Framework 4.8, C#, OleDb, and Microsoft Access.
- Mapped action-based API groups, role sections, IIS rewrite variants, deployment scripts, portal pages, database copies, backups, and historical readbacks.
- Confirmed this directory is a mixed production-support workspace rather than a clean canonical repository.

### Decisions

- The master requirement specification remains the functional authority.
- `docs/` is the durable technical source of truth.
- `PROJECT-CONTEXT.md` remains the operational deployment log.
- Live Site 1 read-back is required before production code changes because similarly named local files may be stale.
- Credentials and sensitive configuration must never be copied into `docs/`.

### Verification

- Confirmed all six required documents were created.
- Confirmed application code, API, database, and production deployment were not modified.
- Reviewed documentation for consistency with current requirements and project context.

### Known issues

- Root-level API sources may lag the most recently deployed Site 1 handler.
- The workspace contains many historical files with ambiguous names.
- History implementation has legacy field/query debt outside the targeted packing fix.
- Production configuration variants enable debug compilation and detailed errors.
- Password storage includes legacy modes that require a planned migration.
- Automated tests and a canonical build pipeline are absent.

### Remaining work

- Synchronize current live Site 1 source into a canonical local tree.
- Complete Phase 3 packing/dispatch regression checks.
- Plan and approve Phase 4 history/report corrections.
- Plan security hardening without disrupting factory users.

### Session rule

After every future coding task, update this file with date, task, modified files, verification, decisions, known issues, and remaining work.

## 2026-07-23 — Deployment and admin runbook

### Task completed

Added useful Site 1 FTP coordinates, credential-source references, known administrator login IDs, access-recovery guidance, and a deployment checklist to `docs/architecture.md`.

### Decision

Connection coordinates and login identifiers may be documented. FTP passwords, administrator passwords, password hashes, and session cookies must remain outside Markdown.

### Verification

- Confirmed the active host, account, remote root, and public URL against existing project context and deployment tooling.
- Confirmed the referenced private deployment scripts exist.
- Confirmed no password value was added to `docs/`.

## 2026-07-24 — Production quantity columns

### Task completed

Added `Panel Qty` and `Board Qty` visibility to local production pending/planner views without changing the database or API contract.

### Files modified

- `planner-portal.html`
- `production-planner.html`
- `live-index.html`
- `live-script.js`
- `docs/phases.md`
- `docs/memory.md`

### Decisions

- Used existing `panel_qty` and `board_qty` fields already returned in planning rows.
- For the main Machine Queue, quantity values are read from the matching planning row when the production row does not carry them directly.
- Persistent production batches and QR label storage were not implemented because that requires explicit API/database approval.

### Verification

- Ran `node --check` on `live-script.js`.
- Extracted and parsed inline JavaScript from `planner-portal.html` and `production-planner.html`.
- Searched changed files for the new quantity labels and helpers.

### Remaining work

- Confirm the live canonical `script.js` source before deployment because this workspace contains multiple readbacks and snapshots.
- Implement production batches and QR printing only after approving the required schema/API scope.

## 2026-07-30 — Adishwar customer type database correction

### Task completed

Changed the live Site 1 database so dealer `Adishwar` uses customer type `ADM`.

### Files modified

- `update-adishwar-adm-customer-type.ps1`
- `docs/memory.md`

### Database change

- Updated one matching dealer row: `Adishwar`.
- Updated existing orders linked to that dealer so their stored customer type also points to `ADM`.
- No schema changes were made.

### Backup and verification

- Downloaded the live database before execution and kept a timestamped backup under `backups/`.
- Ran a dry run before execution.
- Uploaded the changed database only after the local update succeeded.
- Downloaded the live database again after upload and verified:
  - `Adishwar` has `customer_type_code = ADM`.
  - Existing Adishwar orders group under customer type `ADM`.
- Pruned older task-specific dry-run backup folders, keeping the two newest Adishwar-related backup/readback folders.

### Decisions

- The script reads FTP credentials from the existing private deployment script and does not store credentials in documentation.
- Existing orders were updated because `tbl_orders` stores `customer_type_id` separately from `tbl_dealers`.

## 2026-07-30 — Planner export filename cleanup

### Task completed

Renamed the live planner portal export filenames to use readable WIP/date-based names.

### Files modified

- `planner-portal.html`
- `docs/memory.md`

### Production deployment

- Backed up the live `/site1/planner-portal.html` before upload.
- Patched the exact FTP read-back copy because the live planner implementation differs from the root local file.
- Uploaded only `/site1/planner-portal.html`.
- Did not modify the API or database.

### Filename behavior

- Consolidated Excel export now downloads as `WIP Orders Dated DD-MM-YYYY.xlsx`.
- Machine-wise CSV export now downloads as `Machine Wise WIP Orders Dated DD-MM-YYYY.csv`.
- Single-machine CSV export now downloads as `<Station> WIP Orders Dated DD-MM-YYYY.csv`.

### Verification

- Parsed the edited live HTML inline JavaScript with Node.
- Verified FTP read-back SHA-256 matched the uploaded file.
- Verified the public planner URL returns HTTP 200 and contains the new WIP filename logic.

## 2026-07-30 — Admin dealer customer-type change UI

### Task completed

Added an Admin-side tool for changing a dealer's customer type from the UI instead of running a direct database script.

### Files modified/deployed

- `/site1/index.html`
- `/site1/script.js`
- `/site1/api.ashx`
- `/site1/App_Code/PmsApiHandler.cs`
- `docs/memory.md`

### Behavior

- Admin → Masters now includes `Dealer Customer Type Change`.
- Admin can select an active dealer and an active customer type from existing dropdown data.
- Saving updates the dealer master row and existing orders linked to that dealer.
- The save endpoint requires Admin role.
- Unknown customer types are rejected; add the value in Customer Type Master first.

### Verification

- Backed up live files before upload under `backups/admin-customer-type-ui-20260730-160026/`.
- Ran `node --check` for `script.js`.
- Compiled `App_Code/PmsApiHandler.cs` locally with the .NET Framework compiler.
- Uploaded only the affected live files.
- Verified FTP read-back SHA-256 matched each upload.
- Verified the public page returns HTTP 200 and contains the new UI.
- Verified public `script.js` contains the new API mapping.
- Verified unauthenticated endpoint probe returns authorization failure rather than a missing-route error.

### Notes

- No customer type was changed through this deployment.
- To set `M/S KADIWA STUDIO` to `KADIWA`, first ensure `KADIWA` exists in Customer Type Master, then use the new Admin tool.

## 2026-08-05 — Machine QR Scanner

### Task completed

Built and deployed a machine QR scanner page for factory floor order scanning. External QR scanner devices read order stickers and the page auto-prompts action buttons.

### Files modified/deployed

- `qr-scanner.html` — deployed to Site 1
- `deploy-qr-scanner.ps1` — deployment script
- `docs/memory.md`

### Behavior

- Single page with tabs for Hot Press, Cutting, Edgebanding, Drilling, QC.
- External QR scanner acts as keyboard (HID) — hidden input always focused.
- Scan order QR → auto-prompt action panel with Completed / Partial / Rejected buttons.
- Partial and Rejected require mandatory remarks before submission.
- Uses existing `production-action` API endpoint with `order_id`, `station_name`, `action_code`, `remarks`.
- Auto-refresh order list every 30 seconds.
- Pending order count and list displayed per station.
- Login required (Admin or Machine User role).

### Verification

- Public QR Scanner page returns HTTP 200.
- Login, tab switching, and scan flow verified.

### Backup

- Deployed qr-scanner.html to Site 1.
- Deploy script saved locally.

## 2026-08-05 — QR Label Printer and pitch deck

### Task completed

Built and deployed a QR Label Printer page for printing QR code labels for orders. Generated a 100-slide Elenza PMS pitch deck. Cleaned up old backups.

### Files modified/deployed

- `qr-printer.html` — deployed to Site 1
- `ElenzaPMS_PitchDeck.pptx` — generated via `generate_pitch.py`
- `docs/memory.md`

### Behavior

- QR Printer allows selecting orders and printing QR code labels in Roll or Sheet format.
- Label dimensions are configurable (default 60×40 mm).
- QR encodes order number only; label text shows order number.
- Orders loaded from `data_entry.quotations`, `planning.rows`, and `production.rows`.
- Dispatched/Packed/Hold orders excluded.
- Sorted by quotation date (newest first).
- Date range filter with From/To pickers.
- QR generated status tracked in `localStorage` (`elenza_qr_generated`).
- Uses `qrcode-generator` library (sync canvas-based) instead of `qrcodejs` (async, unreliable).
- Fixed `@media print` CSS to hide non-print elements with `display:none`.

### Verification

- Public QR Printer page returns HTTP 200.
- Order loading confirmed working (189 orders via `optimisation.user`).
- Pitch deck generated (100 slides, 207 KB).

### Backup cleanup

- Removed 10 old backups from `backups/` directory.
- Retained 2 most recent backups: `site1-live-backup-20260805` and `project-backup-20260805-151617`.

## 2026-07-30 — KADIWA dealer correction and UI logout fix

### Task completed

Fixed the reported Admin customer-type-change flow issue and directly corrected `M/S KADIWA STUDIO` to customer type `KADIWA`.

### Files modified/deployed

- `/site1/script.js`
- `docs/memory.md`

### Database change

- Changed exact active dealer `M/S KADIWA STUDIO` from customer type `EL` to `KADIWA`.
- Updated 59 existing orders linked to that dealer to customer type `KADIWA`.
- No schema changes were made.

### UI fix

- The dealer customer-type-change UI now matches by exact dealer name, exact dealer code, or a unique normalized match.
- The helper now shows the matched dealer name/code and current/new customer type.
- Removed the immediate full `loadAppState()` refresh after this specific save to avoid the UI appearing to log out after a DB write/app refresh.

### Verification

- Downloaded and backed up the live database before changing it.
- Verified DB read-back after upload:
  - `M/S KADIWA STUDIO` has `customer_type_code = KADIWA`.
  - 59 linked orders group under `KADIWA`.
- Ran `node --check` on the updated `script.js`.
- Uploaded only `/site1/script.js` for the UI fix.
- Verified FTP read-back SHA-256 matched the uploaded script.
- Verified public `script.js` contains the updated matching/helper logic.

## 2026-08-11 — PmsApiHandler.cs restored from backup

### Task completed

Restored live `/site1/App_Code/PmsApiHandler.cs` from the `full-site-backup-20260811` backup because the current handler was reported as not working.

### Files modified/deployed

- `/site1/App_Code/PmsApiHandler.cs`
- `docs/memory.md`

### Backup and source

- Backed up the live handler before restore under `backups/restore-pms-handler-20260811-173025/`.
- Restore source: `backups/full-site-backup-20260811/App_Code/PmsApiHandler.cs`.
- Source/read-back SHA-256: `5065FFFAAA9BE79969B8E37792D8121F1AF851BC00C4802890D73CFD40E9D1AE`.

### Verification

- Local C# compile check passed for the backup handler.
- FTP read-back SHA-256 matched the uploaded file.
- Public main page returned HTTP 200.
- Public `api.ashx?action=session` returned HTTP 200 with JSON `authenticated:false`.
- No database changes were made.
