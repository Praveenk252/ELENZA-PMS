# Delivery Phases

Each planned task belongs to exactly one phase.

## Phase 1 — Core operational system

**Status:** Implemented

Scope:

- Login and role sections
- Dealer and quotation entry
- Order confirmation
- Optimisation and procurement
- Machine production flow
- Packing and dispatch
- Masters, users, reports, and audit
- Microsoft Access persistence

Completion criteria:

- Core users can perform daily operational work.
- Production movement and final dispatch are recorded.

## Phase 2 — Production planner and station workflow

**Status:** Implemented with ongoing fixes

Scope:

- Separate planner workspace
- One current stage
- Machine-wise views grouped by each order's highest-sequence machine
- Board quantity and panel quantity visibility in planner and production pending views
- Priority assignment
- Planner movement, assignment, resequencing, and reapproval
- Filter-aware exports
- Station-based workflow page (`stations.html`) for Hot Press, Cutting, Edgebanding, Drilling, QC
- Drilling 2 merged into Drilling (same logical station)
- Packed/Dispatched orders excluded from station queues and ready lists

Completion criteria:

- Dispatched orders are absent.
- Machine and priority exports match filtered rows.
- Current stage is singular and consistent.
- Board and panel quantities are visible anywhere production teams review pending work.
- Station users see only their own station's pending and completed work.

## Phase 3 — Packing and dispatch hardening

**Status:** Active

Scope:

- Packing portal authorization
- `Packing`/`Packed` compatibility
- Packed-box and balance saves
- Move-to-dispatch completion
- Persistent packing history
- Duplicate-submit prevention
- Dispatch box and balance consistency

Completed:

- Packing login and station compatibility
- Machine-user save authorization
- Packing history after completion
- Full-page review for duplicate saves and history ordering
- Server-side station checks for both "Packing" and "Packed"
- Machine User history visibility (return true)
- History API with customer_name, confirmation_date, packed_boxes, balance_boxes
- Planning row dropdown excludes fully packed orders

Remaining:

- Redeploy PmsApiHandler.cs to Site 1 (live returns 500 currently)
- Update packing-portal.html renderHistory to display new fields (customer_name, confirmation_date, packed_boxes, balance_boxes)
- Manually retest partial-balance saves.
- Verify packing-to-dispatch behavior for main and sub-orders.
- Verify dispatch history and balance behavior with representative orders.

Completion criteria:

- Packing saves work for the assigned user without database renaming.
- Completed orders move to Dispatch and remain visible in history.
- Repeated clicks cannot duplicate actions.

## Phase 4 — History and reporting correctness

**Status:** Planned

Scope:

- Standardize history queries across roles
- Correct legacy history field mappings
- Verify lifecycle current-stage calculation
- Validate packed and dispatched history
- Confirm report filters and exports

Dependencies:

- Stable production movement from Phase 3

Completion criteria:

- Every permitted role sees the correct history.
- Lifecycle reflects the latest valid stage.
- Reports and exports agree with operational state.

## Phase 5 — Security and production configuration

**Status:** Planned

Scope:

- Remove legacy plaintext password modes
- Eliminate any authentication bypass behavior
- Disable debug compilation and detailed errors in production
- Review cookie/session configuration
- Protect sensitive `App_Data` settings
- Review import validation and authorization coverage

Dependencies:

- A synchronized canonical source tree
- Approved maintenance window for authentication changes

Completion criteria:

- Passwords use one secure hashing policy.
- Production errors do not expose internals.
- Mutating endpoints have verified server-side authorization.

## Phase 6 — Source consolidation and automated verification

**Status:** Planned

Scope:

- Create a canonical source directory
- Sync current Site 1 files
- Separate active source, backups, database copies, and diagnostics
- Introduce Git with appropriate ignore rules
- Add repeatable smoke and movement tests
- Document a single deployment/rollback procedure

Completion criteria:

- Engineers can identify the deployable source unambiguously.
- Live/local drift is detected before deployment.
- Critical flows have automated or scripted verification.

## Future roadmap

**Status:** Partially Delivered

Potential work only when explicitly requested:

- ~~Persistent production batches for grouping multiple orders and printing batch/order QR labels~~ — QR Label Printer page deployed (`qr-printer.html`), supports Roll/Sheet format, configurable dimensions, date range filter
- ~~Priority assignment desk~~ — Priority Desk page deployed (`priority-desk.html`), standalone priority + packing date assignment, admin/planner roles, auto-removal on pack
- ~~Production remarks request~~ — Implemented: token-based URL, bulk+individual remarks, replied/unreplied tracking, remarks report with email
- ~~Marketing portal priority~~ — Implemented: badges, row highlighting, filter, report modal
- ~~Station-based workflow~~ — Implemented 2026-08-21: `stations.html` per-station workspace; QR Scanner links replaced everywhere; Drilling 2 merged into Drilling
- ~~Machine-wise grouping by highest sequence~~ — Implemented 2026-08-21 in planner-portal.html exports and views
- Station-ready-orders data backfill — legacy orders lack station date columns; low counts on Cutting/Edgebanding/Drilling. Needs data migration or alternative tracking approach.
- SLA/EDD auto-calculation — Attempted but reverted (compilation issues on shared hosting)
- Barcode scanning
- Inventory valuation
- Accounting integration
- Mobile application
- WhatsApp automation
- Customer portal
- Advanced analytics
