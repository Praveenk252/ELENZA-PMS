# Engineering Memory

Keep this file factual, concise, and free of credentials.

## 2026-08-21 â€” Station workflow, Drilling 2 merge, QC crash fix

### Task completed

Deployed station-based workflow (`stations.html`), merged Drilling 2 into Drilling, fixed the QC crash in `station-ready-orders`, and replaced all QR Scanner quick-links with Stations links.

### Files modified/deployed

- `PmsApiHandler.cs` + `App_Code/PmsApiHandler.cs` â€” removed "Drilling 2" from `StationSequenceOrder` and `StationDateColMap`; added Packed/Dispatched filtering to `HandleStationState`; "Drilling 2" â†’ "Drilling" normalization in `HandleStationUpdate`/`HandleStationState`
- `App_Code/Timestamp.cs` â€” recompile trigger bumped to `2026-08-21 17:12:00.000`
- `stations.html` â€” new per-station workspace (Update Status / Orders at Station / History tabs)
- `index.html`, `dealer-detail.html`, `marketing-assignment.html`, `marketing-portal.html`, `packing-portal.html`, `packing-portal-live-check.html`, `planner-portal.html`, `priority-desk.html`, `qr-printer.html`, `remarks-reply.html` â€” QR Scanner quick-link replaced with Stations link (ðŸ­, stations.html)
- `planner-portal.html` â€” Machine Wise grouping by highest sequence_no; helpers `normalizeStationName()`, `highestSequenceStation()`; priority column 2nd in exports; "Visible Stations" replaces planner stage column

### Key decisions

- Drilling(4) and Drilling 2(5) are the SAME physical station per user confirmation; both users (`mxdr`, `mndr`) write to one logical "Drilling" station
- `drilling2_date` column does not exist in Access DB; mapping it caused OleDb "No value given for one or more required parameters" at QC
- Updated `StationSequenceOrder`: Hot Press, Cutting, Edgebanding, Drilling, QC, Packed, Dispatch
- Updated `StationDateColMap`: hot_press_date, cutting_date, edgebanding_date, drilling_date, qc_date, packed_date, dispatch_date
- Packed/Dispatched orders must never appear in any station dropdown or ready list
- Planner dictation: user types login IDs directly â€” never assume/invent them
- FTP uploads via `FtpWebRequest` (PowerShell `Invoke-WebRequest` does not support FTP)

### Verification

- `station-ready-orders` tested with session cookie for all stations: Hot Press 278, Cutting 2, Edgebanding 1, Drilling 0, QC 1 â€” QC no longer errors
- All station user logins verified PASS (ct, hp, eb1, mxdr, mndr, qc1)
- Station updates from Cutting, Hot Press, Edgebanding, Drilling correctly set planner status to "In Production"
- All 10 HTML pages uploaded via FTP; recompilation triggered via Timestamp.cs
- Full site backup before changes: `backup/site-ftp-20260821-142516.zip` (41 files)
- Git commits pushed through `b1cac31`

### Known issues

- `station-ready-orders` returns low counts for Cutting/Edgebanding/Drilling because most legacy orders lack station date columns (batch/packing flows never set them). Data completeness issue, not code bug. Possible future fix: migrate data or use `visible_stations` field.
- Endpoint requires session cookie auth (not username/password params) â€” test scripts must login first with `-SessionVariable`.

## 2026-08-17 â€” Remarks report and marketing priority

### Task completed

Implemented production remarks report system and marketing portal priority feature.

### Files modified/deployed

- `PmsApiHandler.cs` â€” Added remarks report endpoints (`remarks-report`, `remarks-report-export`, `remarks-report-mail`), remarks request lifecycle (`remarks-request-create`, `remarks-request-info`, `remarks-reply-save`, `remarks-requests-list`, `remarks-request-reminder`, `remarks-request-close`, `remarks-request-delete`), remarks scheduler (`StartRemarksReportScheduler`, `RunRemarksReportSchedulerIfDue`, `TrySendRemarksReport`)
- `Global.asax` â€” Added `PmsApiHandler.StartRemarksReportScheduler()` call in `Application_Start`
- `App_Code/Timestamp.cs` â€” Recompilation trigger for ASP.NET
- `marketing-portal.html` â€” Added priority badges (HIGH/MED/LOW), row highlighting (dark backgrounds), priority filter dropdown, Priority Report modal, modal pre-fill, Clear Priority button, High Priority count stats card
- `remarks-reply.html` â€” Token-based remarks reply page
- `planner-portal.html` â€” Remarks tab for viewing/managing remarks requests

### Key decisions

- Remarks report scheduled at 21:00 IST (hardcoded `RemarksReportHour = 21`)
- `force=1` on `remarks-report-mail` bypasses time check and already-sent check
- SMTP configured via `App_Data/smtp-settings.json`
- Priority row backgrounds: High=`#fecaca` hover=`#fca5a5`, Medium=`#fef08a` hover=`#fde047`
- App pool recompilation via `Timestamp.cs` upload to `App_Code/` (more reliable than web.config comment)

### Verification

- Remarks report endpoints return 200 with correct JSON
- Remarks report mail sent successfully to configured recipients
- Marketing portal priority badges display correctly
- Priority filter and report modal working
- Full site backup created: `backup/site-ftp-20260817-140546/` (43 files)

### Known issues

- SLA/EDD auto-calculation attempted but reverted due to compilation errors on shared hosting
- App pool recompilation via `Timestamp.cs` does not always work â€” sometimes requires manual restore
- Access/OleDb requires explicit parentheses for multiple JOINs: `FROM ((A INNER JOIN B ON ...) INNER JOIN C ON ...) LEFT JOIN D ON ...`

## 2026-08-07 â€” Priority Desk 15-problem fix

### Task completed

Identified and fixed 15 problems in `priority-desk.html`. Backed up original to `backups/priority-desk-backup-20260807/`.

### Fixes applied

1. `toggleSelectAll` â€” now selects from filtered list, not raw `allOrders[0]`
2. `selectAll` checkbox â€” properly toggles selection state
3. Loading spinner â€” shows while API data loads (CSS spinner animation)
4. Session validation â€” `validateSession()` calls `/api.ashx?action=session` on restore from sessionStorage
5. Auto-refresh paused â€” interval skips refresh while action panel is open
6. Escape key â€” closes action panel when open
7. Enter key â€” saves priority from action panel
8. API error messages â€” `loadOrders` catch now shows `e.message` to user
9. Order count indicator â€” shows "X of Y orders" in desk view toolbar
10. Report date defaults â€” auto-fills today's date when switching to report view
11. Report summary â€” adds "No Date" and "No Remarks" count cards
12. Report remarks tooltip â€” adds `title` attribute to remarks cell in report table
13. Mobile responsive â€” action panel details grid stacks to single column, toolbar stacks vertically
14. savePriority validation â€” warns if priority is High but packing date is empty
15. Logout cleanup â€” clears `refreshTimer`, `filteredOrders`, removes keydown listener

### Files modified

- `priority-desk.html` â€” all 15 fixes
- `backups/priority-desk-backup-20260807/priority-desk.html` â€” backup (21KB)

### Verification

- JS syntax validated with `node --check`
- Deployed to Site 1, returns HTTP 200 (23,619 bytes)

## 2026-08-07 â€” Packing portal fixes and history improvements

### Task completed

Fixed multiple packing portal issues for user `pk` (Machine User, station "Packed"). Server-side station checks, history API, portal dropdown filtering, and Machine User history visibility were all corrected.

### Files modified

- `PmsApiHandler.cs` â€” `packing-boxes-set` and `production-balance-save` now allow both "Packing" and "Packed" stations; `EnsurePackingQueueEntryForPortal` falls back through Packing â†’ Packed â†’ user's station; `BuildHistoryStandaloneState` joins tbl_dealers for customer_name, includes confirmation_date, packed_boxes (from tbl_dispatch_boxes), balance_boxes (from tbl_orders); `IsHistoryVisibleToUser` changed to `return true` for Machine User (station filter was too restrictive)
- `packing-portal.html` â€” Planning rows filtered by prodLookup to exclude fully packed orders (box_count > 0 AND balance = 0); history table headers updated to Order no / Customer Name / Confirmation Date / Pack Qty / Balance Qty; station_name filter removed from history rows

### Key decisions

- `packing_balance_box_qty` read from `tbl_orders` (not `tbl_production_planner`) via main query
- Machine User history visibility changed to `return true` because `station_name` from `VisibleStationNames` uses "-" as placeholder, failing station-match logic
- Planning rows do not carry `box_count` or `packing_balance_box_qty` â€” used production rows lookup (prodLookup) to filter fully packed
- History grouped to one latest row per order, preserving API newest-first order

### Verification

- Local `PmsApiHandler.cs` compiles cleanly (no `balanceLookup` references remain)
- **Note:** Live API currently returns 500 for `history-state` and `app-state` â€” likely the deployed version has stale code; requires redeploy via `deploy-qr-scanner.ps1` or manual FTP upload of PmsApiHandler.cs

## 2026-08-07 â€” Priority Desk deployed

### Task completed

Built and deployed Priority Desk page to Site 1. Added `priority_date` column to `tbl_production_planner` via `EnsureSchema` ALTER TABLE. Added `priority-desk-state` and `priority-report` API actions. Modified `HandlePlannerSave` to accept and persist `priority_date`. Updated login page Quick Access with Priority Desk link.

### Files modified

- `PmsApiHandler.cs` â€” Added `priority_date` schema, planner-save support, new API actions
- `priority-desk.html` â€” New standalone page for priority assignment
- `index.html` â€” Added Priority Desk Quick Access link
- `styles.css` â€” Updated quick link styles
- `PROJECT-CONTEXT.md` â€” Added Priority Desk entry
- `docs/phases.md` â€” Updated future roadmap
- `docs/memory.md` â€” This entry

### Key decisions

- Only "High" priority (no Medium/Low) per user requirement
- Uses existing `tbl_production_planner` table with new `priority_date` column
- Manual packing date pick (not auto-populated)
- Auto-removal when packed (workflow_stage_code=DISPATCH_READY)
- Fixed API typo: `BuildStatusLookup` â†’ `LoadStatusLookup` in both new handlers

### Verification

- API returns 200 for `priority-desk-state` and `priority-report`
- Priority Desk page loads at `https://<live-host>/priority-desk.html`
- Login page has Priority Desk Quick Access link
- Test login: `admin.user` / `1` or `planner.user` / `1`

## 2026-07-23 â€” Project Blueprint established

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

## 2026-07-23 â€” Deployment and admin runbook

### Task completed

Added useful Site 1 FTP coordinates, credential-source references, known administrator login IDs, access-recovery guidance, and a deployment checklist to `docs/architecture.md`.

### Decision

Connection coordinates and login identifiers may be documented. FTP passwords, administrator passwords, password hashes, and session cookies must remain outside Markdown.

### Verification

- Confirmed the active host, account, remote root, and public URL against existing project context and deployment tooling.
- Confirmed the referenced private deployment scripts exist.
- Confirmed no password value was added to `docs/`.

## 2026-07-24 â€” Production quantity columns

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

## 2026-07-30 â€” Adishwar customer type database correction

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

## 2026-07-30 â€” Planner export filename cleanup

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

## 2026-07-30 â€” Admin dealer customer-type change UI

### Task completed

Added an Admin-side tool for changing a dealer's customer type from the UI instead of running a direct database script.

### Files modified/deployed

- `/site1/index.html`
- `/site1/script.js`
- `/site1/api.ashx`
- `/site1/App_Code/PmsApiHandler.cs`
- `docs/memory.md`

### Behavior

- Admin â†’ Masters now includes `Dealer Customer Type Change`.
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

## 2026-08-05 â€” Machine QR Scanner

### Task completed

Built and deployed a machine QR scanner page for factory floor order scanning. External QR scanner devices read order stickers and the page auto-prompts action buttons.

### Files modified/deployed

- `qr-scanner.html` â€” deployed to Site 1
- `deploy-qr-scanner.ps1` â€” deployment script
- `docs/memory.md`

### Behavior

- Single page with tabs for Hot Press, Cutting, Edgebanding, Drilling, QC.
- External QR scanner acts as keyboard (HID) â€” hidden input always focused.
- Scan order QR â†’ auto-prompt action panel with Completed / Partial / Rejected buttons.
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

## 2026-08-05 â€” QR Label Printer and pitch deck

### Task completed

Built and deployed a QR Label Printer page for printing QR code labels for orders. Generated a 100-slide Elenza PMS pitch deck. Cleaned up old backups.

### Files modified/deployed

- `qr-printer.html` â€” deployed to Site 1
- `ElenzaPMS_PitchDeck.pptx` â€” generated via `generate_pitch.py`
- `docs/memory.md`

### Behavior

- QR Printer allows selecting orders and printing QR code labels in Roll or Sheet format.
- Label dimensions are configurable (default 60Ã—40 mm).
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

## 2026-07-30 â€” KADIWA dealer correction and UI logout fix

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

## 2026-08-11 â€” PmsApiHandler.cs restored from backup

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
