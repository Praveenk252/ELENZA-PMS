# Architecture

## System shape

Elenza PMS is a server-rendered/static-browser application with a single ASP.NET JSON API and a Microsoft Access database.

```text
Browser
  ├─ Main HTML/CSS/JavaScript workspace
  ├─ Production planner portal
  ├─ Packing portal
  ├─ QR Label Printer
  └─ Machine QR Scanner
          │ same-origin HTTP + session cookie
          ▼
ASP.NET Framework 4.8
  ├─ api.ashx
  ├─ api.aspx
  └─ App_Code/PmsApiHandler.cs
          │ OleDb
          ▼
App_Data/elenza_pms.accdb
```

## Technology stack

- **Frontend:** HTML, CSS, and browser JavaScript
- **Backend:** C# ASP.NET Framework 4.8
- **API style:** action-based JSON endpoints through `api.ashx?action=...`
- **Session:** ASP.NET session state
- **Serialization:** `JavaScriptSerializer`
- **Database:** Microsoft Access through `System.Data.OleDb`
- **Hosting:** Windows/IIS-compatible shared ASP.NET hosting
- **Deployment:** FTP scripts and manual verified uploads
- **Email:** `System.Net.Mail` with settings stored under `App_Data`

There is no framework build pipeline or canonical `.sln`/`.csproj` in this support workspace.

## Repository structure

This directory is a production-support workspace and contains both current and historical artifacts.

```text
/
  ElenzaIndia-Production-Management-Requirement-Specification.md
  PROJECT-CONTEXT.md
  README.md
  VERSIONING.md
  docs/
  packing-portal.html
  planner-portal.html
  production-planner.html
  PmsApiHandler.cs
  api.aspx
  *-web.config
  *.ps1
  backups/
  work/
  node_modules/
  numerous live/readback/verify/restore snapshots
  multiple .accdb copies
```

Filename prefixes such as `live-`, `root-`, `ftp-`, `public-`, `verify-`, `restore-`, and `*-current` do not reliably prove that a file is the latest production version.

## Canonical-source policy

Use these sources in order:

1. Explicit user instruction
2. Master requirement specification
3. `docs/` documentation
4. `PROJECT-CONTEXT.md` for deployment state and recent decisions
5. Verified read-back of the active Site 1 files
6. Local implementation files
7. Historical snapshots and diagnostic copies

Before modifying production code, download or read back the exact live target and compare it with the intended local source. The latest packing portal is local, but recent API compatibility/history changes were deployed from temporary working copies; root-level API files may therefore lag production.

## Frontend

### Main application

The main interface is a large HTML/JavaScript application served from the site root. IIS rewrite rules may serve `script.js` dynamically through `api.ashx?action=script`.

### Planner

Planner pages provide:

- All-orders, machine-wise, and priority views
- One displayed current stage
- Priority editing
- Assignment, resequencing, and reapproval actions
- Filter-aware exports

IIS may rewrite `planner-portal.html` to `production-planner.html`; confirm the active host configuration before editing either file.

### Packing

`packing-portal.html` is a separate login/workspace. It:

- Accepts Admin or the packing Machine User
- Supports the live station labels `Packing` and `Packed`
- Submits the authenticated user's assigned station
- Saves packed-box count and balance
- Completes orders with zero balance
- Shows station history after orders move to Dispatch
- Prevents duplicate saves while a request sequence is running

### QR Label Printer

`qr-printer.html` is a standalone page for printing QR code labels. It:

- Loads orders from multiple API sources (`data_entry.quotations`, `planning.rows`, `production.rows`)
- Excludes Dispatched/Packed/Hold orders
- Supports Roll and Sheet label formats
- Configurable label dimensions (default 60×40 mm)
- QR encodes order number; label text shows order number
- Date range filter with From/To pickers
- Tracks QR generated status in `localStorage`
- Uses `qrcode-generator` library for sync canvas-based QR rendering
- Sorted by quotation date (newest first)

### Machine QR Scanner

`qr-scanner.html` is a single-page machine scanner for factory floor use. It:

- Provides tabs for Hot Press, Cutting, Edgebanding, Drilling, QC
- Accepts external QR scanner input (scanner acts as keyboard/HID device)
- Hidden input field always focused to capture scanned order numbers
- Auto-prompts action panel after scan with Completed / Partial / Rejected buttons
- Partial and Rejected require mandatory remarks
- Uses existing `production-action` API endpoint
- Auto-refreshes order list every 30 seconds
- Shows pending order count and list per station
- Requires Admin or Machine User login

## Backend and API

`PmsApiHandler` implements a large action dispatcher. Major endpoint groups include:

- Authentication: `session`, `login-init`, `login`, `logout`
- State: `app-state`, `history-state`
- Dealers and quotations
- Confirmation, optimisation, and procurement
- Planner movement, priority, sequence, and station actions
- Production, packing, and dispatch actions
- Masters, machines, and users
- Report email generation and status

Authorization is enforced in handler methods with role checks and station comparisons.

## Authentication and authorization

The session stores `user_id`; each request reloads the user, role, home section, and assigned station from the database.

Role sections currently include:

- Admin
- Data Entry
- Quotation User
- Marketing User
- Optimisation User
- Procurement User
- Production Planner User
- Machine User
- Dispatch User
- Management

Machine updates must pass both role and assigned-station checks.

## Database

The Access database stores:

- Users and roles
- Dealers and quotations/orders
- Confirmations, optimisation, and procurement
- Vendors and masters
- Machines and sequence profiles
- Station queues
- Production and order history
- Dispatch state and boxes
- Audit logs
- Email/report state

The API contains schema-readiness and compatibility logic. Schema changes can occur when the handler initializes, so deploying a different handler version can affect the database even when no manual database upload occurs.

## Production movement

```text
Quotation
  → Confirmed
  → Optimised
  → Procurement / Material Received
  → Production sequence
  → Packing / Packed
  → Dispatch
  → Dispatched
```

Sequence profiles determine station order. Station queue visibility supports partial completion, while planner-stage calculation provides one current displayed stage.

## Configuration

Observed IIS configuration includes:

- ASP.NET Framework 4.8
- Integrated handler mappings for `.ashx`
- URL rewrite from `api.ashx` to `api.aspx` in one configuration variant
- Dynamic `script.js` rewrite in another configuration variant
- Detailed errors and debug compilation enabled in diagnostic/current local configs
- No-cache headers

Do not assume a local `web.config` variant is active. Read back the active Site 1 configuration before changing it.

## Deployment and rollback

- Active target: Site 1
- Public URL: `http://[removed]-site1.ktempurl.com/`
- FTP host: `[removed]`
- FTP account: `[removed]`
- FTP mode used by existing tooling: passive binary FTP
- Deployment root: `/site1`
- Markdown files must not be uploaded.
- Back up each live target before upload.
- Verify upload with FTP read-back hash and public HTTP behavior.
- Preserve the two newest relevant backups.
- Do not deploy to the paused host without explicit instruction.

### Credential location

Passwords are deliberately not duplicated in Markdown. Retrieve the current Site 1 FTP credential from the existing private deployment configuration:

- `C:\Users\Praveen\Documents\Codex\2026-07-16\c-users-praveen-documents-codex-2026\backup-site1-current.ps1`
- `C:\Users\Praveen\Documents\Codex\2026-07-16\c-users-praveen-documents-codex-2026\deploy-site1-lean.ps1`

Use the credential only through deployment tooling or an in-memory credential object. Do not print it in terminal output, chat, documentation, logs, or generated reports.

### Administrative access

Known seeded administrator login IDs:

- `admin.user`
- `asha.admin`

Administrator capability includes all application sections: data entry, optimisation, procurement, planner, production, dispatch, reports, history, email log, masters, users, and settings.

Login IDs are not proof that an account is active in the current live database. Verify the account through the live session/login endpoint or the Users screen before relying on it.

Administrator passwords are stored in the live Access database and must not be copied into documentation. If access is lost:

1. Prefer another active Admin account to reset the user through the Users screen.
2. If no Admin account works, use the existing password-reset tooling only with explicit approval and a current database backup.
3. Verify login in an isolated session after reset.
4. Record the reset event without recording the password.

### Deployment checklist

1. Confirm Site 1 is still the active target in `PROJECT-CONTEXT.md`.
2. Read back the exact remote file from `/site1`.
3. Save the original under a timestamped local backup.
4. Patch the read-back or a verified matching canonical source.
5. Upload only approved files; never upload `.md`.
6. Download the uploaded file and compare SHA-256.
7. Verify the public page or API endpoint.
8. Keep the two newest relevant backups and update `docs/memory.md`.

Never place passwords or reusable session cookies in `docs/`.

## Third-party services

- Shared ASP.NET hosting
- SMTP server configured through private `App_Data` settings
- Google Fonts on standalone portal pages

No other third-party runtime dependency should be assumed without code evidence.

## Known architecture risks

- Mixed historical and current files in one directory
- Large monolithic C# and JavaScript files
- Access concurrency and file-locking constraints
- Action-based API without generated contract/types
- Limited automated test coverage
- Diagnostic error exposure in configuration
- Some seeded/sample users use legacy password storage modes
- Live/local API drift
- Similar concepts named `Packing` and `Packed`
