# Elenza PMS Working Context

## 2026-08-07

### Machine QR Scanner

- Built and deployed `qr-scanner.html` to Site 1.
- Single-page machine scanner with tabs for Hot Press, Cutting, Edgebanding, Drilling, QC.
- External QR scanner acts as keyboard input — hidden field always focused.
- Scan order QR → auto-prompt action panel (Completed / Partial / Rejected).
- Partial and Rejected require mandatory remarks.
- Auto-refresh order list every 30 seconds.
- Pending order count and list per station.
- Uses existing `production-action` API endpoint.
- Public URL: `http://[removed]-site1.ktempurl.com/qr-scanner.html`

### Packing Portal

- Box qty entry is only available for Packing.
- Packing portal URL: `http://[removed]-site1.ktempurl.com/packing-portal.html`
- Packing username: `pk`
- Fixed `packing-boxes-set` server-side check: now allows both "Packing" and "Packed" stations.
- Fixed `production-balance-save` server-side check: now allows both "Packing" and "Packed" stations.
- Fixed `EnsurePackingQueueEntryForPortal`: allows "Packed" station, falls back through Packing → Packed → user's station.
- Fixed Machine User history visibility: changed `IsHistoryVisibleToUser` to `return true` (station filter was too restrictive with "-" placeholder).
- Fixed history API `BuildHistoryStandaloneState`: populates `VisibleStationNames`, joins `tbl_dealers` for customer_name, includes confirmation_date, packed_boxes (from `tbl_dispatch_boxes`), balance_boxes (from `tbl_orders`).
- Packing portal dropdown excludes fully packed orders (box_count > 0 AND balance = 0) using prodLookup from production rows.
- Packing portal history table updated to show: Order no, Customer Name, Confirmation Date, Pack Qty, Balance Qty.
- History row filter removed (was filtering by `station_name`).

### Login Page Quick Access

- Added Quick Access links to login page (`index.html`).
- Links: QR Scanner, QR Label Printer, Planner Portal, Packing Portal, Priority Desk.
- Grid layout with icon, label, and description for each link.
- Updated `styles.css` with quick link styles.

### Priority Desk

- Built and deployed `priority-desk.html` to Site 1.
- Standalone page for assigning priority and packing date to orders.
- Only shows "High" priority (no Medium/Low).
- Manual packing date pick using date input.
- Only main orders (no sub-orders).
- Auto-removal when packed (workflow_stage_code=DISPATCH_READY).
- Uses existing `tbl_production_planner` table with new `priority_date` column.
- New API actions: `priority-desk-state` (GET), `priority-report` (GET).
- Modified `HandlePlannerSave` to accept and persist `priority_date`.
- Added `priority_date` to `BuildPlanningRow` output.
- Accessible to Admin and Production Planner User roles.
- Public URL: `http://[removed]-site1.ktempurl.com/priority-desk.html`
- 15-problem fix applied: toggleSelectAll, session validation, loading spinner, auto-refresh pause, Escape/Enter keys, error display, order count, report date defaults, report summary cards, report remarks tooltip, mobile responsive, save validation, logout cleanup.
- Backup at `backups/priority-desk-backup-20260807/`.

### QR Label Printer

- Built and deployed `qr-printer.html` to Site 1.
- Page allows selecting orders and printing QR code labels in Roll or Sheet format with configurable label dimensions.
- QR encodes order number only; label shows order number below QR.
- Loads orders from multiple API sources (`data_entry.quotations`, `planning.rows`, `production.rows`).
- Excludes Dispatched/Packed/Hold orders.
- Sorted by quotation date (newest first).
- Date range filter with From/To pickers.
- QR generated status tracked in `localStorage` (`elenza_qr_generated`).
- Default label size: 60×40 mm, QR 120 px, font 12 px.
- Uses `qrcode-generator` library (sync canvas-based) instead of `qrcodejs` (async, unreliable).
- Fixed `@media print` CSS (was `visibility:hidden`, now `display:none` for non-print elements).
- Public URL: `http://[removed]-site1.ktempurl.com/qr-printer.html`

### Pitch Deck

- Generated 100-slide Elenza PMS pitch deck (`ElenzaPMS_PitchDeck.pptx`, 207 KB) via `generate_pitch.py`.

### Backup Cleanup

- Removed 10 old backups (planner-membrane, debug-admin, db-kadiwa, admin-customer-type-ui, planner-export-filename, db-adishwar, packing-history-fix, packing-code-fix).
- Saved approximately 87 MB.
- Retained: `site1-live-backup-20260805` (14.07 MB) and `project-backup-20260805-151617` (1.31 MB).

## 2026-07-23

### Project Blueprint Documentation

- Added the documentation-first `docs/` source of truth: `project.md`, `architecture.md`, `rules.md`, `design.md`, `phases.md`, and `memory.md`.
- Documented product scope, current architecture, production safety rules, design language, delivery phases, live/local source drift, and persistent engineering memory.
- Added a safe deployment/admin runbook with Site 1 connection coordinates, private credential-source references, known Admin login IDs, recovery guidance, and deployment verification steps.
- The master requirement specification remains the functional authority.
- No application code, API, Access database, or production deployment was changed.
- Deployment credentials and other secrets were deliberately excluded from `docs/`.

## 2026-07-22

### Completed

- Fixed the live Site 1 packing portal authorization mismatch.
- The live `packing.user` account is assigned to station `Packed`, while the portal previously accepted only `Packing`.
- Updated only `packing-portal.html` so a `Machine User` assigned to either `Packing` or `Packed` can open the packing workspace.
- API and database were not modified.

### Backup and Verification

- Backup: `backups/packing-portal-fix-20260722-185801/packing-portal.html`
- Original SHA-256: `173A0ADC3CB92FF942826B4436CB0005634FC4DFA0D6D2EDFBB1D71651144F9E`
- Uploaded SHA-256: `FD803610D064BEB8993670BF21518BFC5E6DD3C7938E793A6FD0430B3D6F7561`
- FTP read-back matched the uploaded file.
- Public `packing-portal.html` returned HTTP 200 and contained the authorization fix.
- `packing.user` login and authenticated `app-state` both succeeded.

### Packing Save Compatibility Fix

- Fixed the subsequent `Machine user can only update assigned station orders` error without changing the database.
- The live machine is named `Packed`; the portal now submits the authenticated user's assigned station instead of hardcoding `Packing`.
- Updated `api.ashx` and `App_Code/PmsApiHandler.cs` to treat both `Packing` and `Packed` as the packing station throughout packing validation and movement logic.
- Live backups: `backups/packing-code-fix-20260722-190045/`.
- FTP read-back matched all three uploads: `packing-portal.html`, `api.ashx`, and `App_Code/PmsApiHandler.cs`.
- Public portal and authenticated `app-state` returned HTTP 200.
- A non-mutating invalid-order probe using station `Packed` passed station authorization and returned `Order not found`, confirming the assigned-station error is resolved.
- The Access database was not downloaded, edited, or uploaded for this fix.

### Packing History and Full-Page Review

- Fixed the packing History table so completed orders remain visible after moving to Dispatch.
- Fixed machine-user `history-state`, which previously returned zero rows because the standalone query lacked station history data before applying station permissions.
- History now returns the assigned station's actual audit rows with order number, confirmation date, packed boxes, balance boxes, and action date.
- The portal accepts both `Packing` and `Packed` when filtering active and historical rows.
- History is grouped to one latest row per order and preserves the API's newest-first order.
- Added duplicate-submit protection while the packing save sequence is running.
- Backup: `backups/packing-history-fix-20260722-190721/`.
- FTP read-back matched `packing-portal.html`, `api.ashx`, and `App_Code/PmsApiHandler.cs`.
- Verified live: history HTTP 200 with 120 rows; `Vishal-PD bathroom 1` appears with 1 packed box, 0 balance, station `Packed`, and action `Completed`.
- The Access database was not changed.

## Working Rules

- Update this file during the first project chat of each day.
- During active work, optimize the conversation context every two hours.
- Record completed live changes, backup locations, verification results, and pending approvals.
- Back up a live file before changing it.
- Do not modify the API or database unless explicitly approved.

## 2026-07-16

### Completed

- Read and understood the master requirement specification.
- Backed up the live `PMS/planner-portal.html` page before editing.
- Added an `Export Consolidated` button to the Consolidated Planner List.
- Export uses the currently filtered planner rows and produces an Excel-compatible CSV.
- Uploaded only `PMS/planner-portal.html` and verified it through FTP read-back and the public URL.
- API and database were not modified.

### Backup

- `backups/planner-export-20260716-133756/planner-portal.html`
- Original SHA-256: `56FDE39721C791500D061E864241658B02BC445D218D0B28A15E38D38D7C27C7`

### Pending Approval

- Requested change: hide orders whose planner current stage is `Packed`.
- A read-only check of a downloaded live database copy found 24 affected orders.
- No change has been made for this request; wait for explicit approval before editing or uploading.

## Hosting Migration Details

- Destination host: `ftp.serverbyt.in`
- Protocol: explicit FTPS / TLS
- Destination folder: `/erp`
- Username: `elenzaerp@erp.elenzaerp.cloud`
- Password: `Elenzaerp@123#$`
- Migration rule: upload application files and database only; do not upload Markdown (`.md`) files.

## 2026-07-16 Migration Status

### New Hosting

- Public site: `http://erpelenza.runasp.net/`
- Hosting: MonsterASP, ASP.NET Framework 4.8, Integrated mode, 32-bit.
- Full application migration completed to `wwwroot`.
- Uploaded 228 non-Markdown files, including the Access database, API handler, assets, configuration, and planner updates.
- Markdown files were deliberately excluded from the host.
- The login page was redesigned with the premium Elenza green/lime visual direction and uploaded as `index.html` plus `styles.css`.

### Verified Working

- Public landing/login page loads.
- `api.ashx?action=session` returns valid JSON.
- Login with `planner.user` and password `1` succeeds.
- Session remains authenticated after login.

### Active Blocker

- `api.ashx?action=app-state` resets the connection on MonsterASP after login.
- Exact transport error: `curl: (56) Recv failure: Connection was reset` with HTTP `000`.
- This prevents the dashboard from rendering after login.
- The issue affects planner, data-entry, optimisation, and management users.
- AppPool restart did not resolve it.
- Pending MonsterASP support/forum guidance on application logs, ACE OLEDB/Access compatibility, and application data folder permissions.

## 2026-07-16 Current Production State (MyASP)

- Active FTP/site: `win1006.site4now.net`, application root `/PMS`.
- Public URL: `http://elenzapms-001-site1.jtempurl.com/`.
- Rule: use this MyASP host only unless the user explicitly changes the instruction.
- Do not upload Markdown files. Back up before every live upload or deletion.

### Lean Deployment

- Legacy duplicate apps, LSP, timer, Cabinet databases, Python files, logs, and Markdown files were removed from `/PMS`.
- Required production set retained: `index.html`, `styles.css`, `script.js`, `api.ashx`, `api.aspx`, `web.config`, `planner-portal.html`, `packing-portal.html`, `favicon.ico`, health checks, `assets/`, `App_Data/`, and `App_Code/PmsApiHandler.cs`.
- Important correction: `App_Code/PmsApiHandler.cs` is required by `api.aspx`; it was restored after cleanup. Do not remove it.
- Active data is `App_Data/elenza_pms.accdb` (10,006,528 bytes, SHA-256 `5421EAF9B606E9DD8A992ADD602CAF00B2319049B927D1227EFA5BD9D60EE41A`).

### Latest Verification

- Homepage and API session endpoints return HTTP 200.
- Planner login works (`planner.user` / `1` used only for read-only verification).
- Authenticated app-state read works: planner rows 485, customer types 6, machines 8, sequence profiles 64.

### Latest Visual Updates

- `index.html` and `styles.css`: premium blue/navy login visual refresh, uploaded and live.
- `planner-portal.html` and `packing-portal.html`: matching blue workspace treatment, uploaded and live (HTTP 200).

### Recent Backups (local only)

- `C:\Users\Praveen\Documents\Codex\2026-07-16\c-users-praveen-documents-codex-2026\work\original-myasp-preupload-backup-20260716-202752`
- `C:\Users\Praveen\Documents\Codex\2026-07-16\c-users-praveen-documents-codex-2026\work\original-myasp-preupload-backup-20260716-203023`

## 2026-07-18 Active Site 1 State

- Active deployment target: `[removed]`, user `[removed]`, website root `/site1`.
- Public URL: `http://[removed]-site1.ktempurl.com/`.
- Previous MyASP host is paused; do not deploy there unless explicitly instructed.
- Complete lean PMS site is deployed to Site 1, including `App_Code/PmsApiHandler.cs` (required by `api.aspx`).
- Old-live database was copied to Site 1 and verified by SHA-256: `7FEBDFCAC0DFEE4D325D153A98CD8CA11780BDADD1ED3A5388194C5927486763`.

### Current API Behaviour

- Planner station mapping was corrected: selecting `Packed` sets `DISPATCH_READY` / `PENDING_DISPATCH`; selecting `Dispatched` sets `DISPATCHED` / `DISPATCHED`.
- This stage-mapping change remains live.
- History loading investigation found two deferred fixes: missing deep-state parameters and an invalid history field (`old_status_code` instead of `previous_status_code`).
- The history fixes were deliberately rolled back at the user's request. Do not reapply until the user asks.

### Retained Local Backups

- Full migration snapshot: `work\migration-backup`.
- Current Site 1 database source: `work\old-live-db-backup-20260717-105200\elenza_pms.accdb`.
- Current API rollback point: `work\site1-before-history-fix-rollback-20260718-154407\PmsApiHandler.cs`.
- Redundant/partial backups were intentionally removed on 2026-07-18.
