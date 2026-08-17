# Elenza PMS Working Context

## 2026-08-17

### Production Remarks Request — Implemented

- `remarks-request-create` creates token-based request records in `tbl_remarks_requests`.
- `remarks-reply.html?token=...` — planner opens URL, logs in, enters remarks per order or bulk.
- Saves to `tbl_remarks_requests` and `tbl_remarks_replies` tables.
- `remarks-requests-list`, `remarks-request-info`, `remarks-reply-save` endpoints all working.
- `remarks-request-reminder` sends reminder notifications.
- `remarks-request-close` and `remarks-request-delete` manage lifecycle.
- Replied/unreplied tracking per order.

### Remarks Report — Implemented

- `remarks-report` endpoint returns done/pending rows with order details.
- `remarks-report-export` exports to Excel via server-side HTML table.
- `remarks-report-mail` sends HTML email summary to configured recipients.
- Scheduled daily at 21:00 IST via Global.asax Timer (`RunRemarksReportSchedulerIfDue`).
- `force=1` parameter bypasses time check and already-sent check.
- SMTP configured via `App_Data/smtp-settings.json` with `to_emails`, `enabled`, `host`, `port`, etc.

### Marketing Portal Priority Feature — Implemented

- Priority badges: HIGH (red), MED (yellow), LOW (blue) displayed on In Production tab.
- Row highlighting: High=`#fecaca` hover=`#fca5a5`, Medium=`#fef08a` hover=`#fde047`.
- Priority filter dropdown in toolbar (All/High/Medium/Low).
- Priority Report modal with summary cards and per-order table.
- Priority modal pre-fills current priority when editing.
- Clear Priority button to remove priority assignment.
- Stats card shows High Priority count.
- `priority-desk-state` and `priority-report` API endpoints.

### Marketing Portal — 4-Tab Layout

- Rebuilt `marketing-portal.html` with 4 tabs: My Dealers, In Production, Packed, Dispatched.
- In Production: sorted by dealer, sub-orders excluded, columns (Checkbox, Order #, Confirmation, Order Type, Dealer, Customer, Status, EDD, Actions).
- Packed: added Box Qty and Balance Box columns from `packing_balance_box_qty`.
- Dispatched: shows dispatch rows for Marketing User.
- `planner-save` endpoint allows Marketing User role for priority setting.

### WhatsApp Messaging

- Each tab has WhatsApp dropdown: Status Ask, Need Urgent, Custom Message.
- Packed tab has different options: Ready for Dispatch, Box Balance, Custom Message.
- All tabs have "Just copy to clipboard" checkbox (desktop only, unticked by default).
- `navigator.share()` with clipboard fallback. Added `fallbackCopy()` using `document.execCommand('copy')` for non-HTTPS environments.
- Date format: `fmtDate()` outputs DD-Mon-YY (e.g., 11-Aug-26). Handles any input format.

### EDD Change Request

- EDD change request modal copies text to clipboard for WhatsApp sharing.

### App Pool Recompilation Trick

- Upload a `Timestamp.cs` file to `App_Code/` to force ASP.NET dynamic compilation.
- Delete after recompile confirmed. This is more reliable than `web.config` comment changes.

### Backup

- Full FTP site backup: `backup/site-ftp-20260817-140546/` (43 files from `[removed]/site1/`).
- FTP host: `[removed]`, user: `[removed]`.
- Old FTP host `win1006.site4now.net` returns 530 Not logged in.

### Git

- Committed and pushed to GitHub: `https://github.com/Praveenk252/ELENZA-PMS.git`

---

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

---

## 2026-07-23

### Project Blueprint Documentation

- Added the documentation-first `docs/` source of truth: `project.md`, `architecture.md`, `rules.md`, `design.md`, `phases.md`, and `memory.md`.
- Documented product scope, current architecture, production safety rules, design language, delivery phases, live/local source drift, and persistent engineering memory.
- Added a safe deployment/admin runbook with Site 1 connection coordinates, private credential-source references, known Admin login IDs, recovery guidance, and deployment verification steps.
- The master requirement specification remains the functional authority.
- No application code, API, Access database, or production deployment was changed.
- Deployment credentials and other secrets were deliberately excluded from `docs/`.

---

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

---

## Working Rules

- Update this file during the first project chat of each day.
- During active work, optimize the conversation context every two hours.
- Record completed live changes, backup locations, verification results, and pending approvals.
- Back up a live file before changing it.
- Do not modify the API or database unless explicitly approved.

## Hosting & Deployment

- Active FTP/site: `[removed]`, user `[removed]`, website root `/site1`.
- Public URL: `http://[removed]-site1.ktempurl.com/`.
- Old FTP host `win1006.site4now.net` returns 530 Not logged in — do not use.
- Database: `App_Data/elenza_pms.accdb` on Site 1.
- API handler: `App_Code/PmsApiHandler.cs` (required by `api.aspx`).
- Git: `https://github.com/Praveenk252/ELENZA-PMS.git`
- Backup skill: `.agents/skills/backup/SKILL.md` — run `backup the site` to download full FTP.

## 2026-08-11 — PmsApiHandler.cs restore

- Restored live `/site1/App_Code/PmsApiHandler.cs` from `backups/full-site-backup-20260811/App_Code/PmsApiHandler.cs`.
- Reason: live API handler was reported as not working.
- Live handler was backed up before restore under `backups/restore-pms-handler-20260811-173025/`.
- Restore source/read-back SHA-256: `5065FFFAAA9BE79969B8E37792D8121F1AF851BC00C4802890D73CFD40E9D1AE`.
- Verification after upload:
  - Main site returned HTTP 200.
  - `api.ashx?action=session` returned HTTP 200 with JSON `authenticated:false`.
- Database was not changed.

## Key Files

| File | Purpose |
|---|---|
| `marketing-portal.html` | Marketing 4-tab portal (My Dealers, In Production, Packed, Dispatched) with priority UI |
| `planner-portal.html` | Planner portal with remarks tab |
| `packing-portal.html` | Packing portal |
| `qr-scanner.html` | Machine QR scanner |
| `qr-printer.html` | QR label printer |
| `priority-desk.html` | Priority desk |
| `remarks-reply.html` | Remarks reply page (token-based URL) |
| `PmsApiHandler.cs` | API handler (also uploaded as `App_Code/PmsApiHandler.cs`) |
| `Global.asax` | App startup with remarks report scheduler |
| `App_Code/Timestamp.cs` | Recompilation trigger for ASP.NET |

## Database Key Tables

- `tbl_orders` — orders with workflow_stage_code, dispatch_status_code, packing_balance_box_qty, dispatch_balance_box_qty
- `tbl_production_planner` — planner priority, EDD (sla_date), priority, priority_date, planner_remarks
- `tbl_order_station_queue` — station queue with status, remarks
- `tbl_dealers` — dealer info with marketing_owner
- `tbl_users` — users with role_id
- `tbl_dispatch_boxes` — box tracking per order
- `tbl_remarks_requests` — WhatsApp remarks requests with token
- `tbl_remarks_replies` — Planner remarks replies per order
- `tbl_mail_reports` — Email send log for reports
