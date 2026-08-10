# Requirement Specification

## ElenzaIndia.com Production Management System

### Project Type
Modular Interiors B2B

### Technology Direction
- HTML
- CSS
- Basic JavaScript where required
- Microsoft Access database in `.accdb` format

## 1. Product Goal

Build a very minimal production management system for ElenzaIndia.com modular interiors B2B operations.

The system must be data-entry-first.

Primary priorities:

1. Fast data entry
2. Correct order tracking
3. Machine-wise production movement
4. Simple reports

Dashboard is not required in the first version.

## 2. Branding and UI Direction

Brand: ElenzaIndia.com

The system must use the ElenzaIndia.com logo.

The system UI must use the current `www.elenzaindia.com` colour scheme.

Brand colour palette to follow:

- Primary blue: `#046BD2`
- Secondary blue / hover blue: `#045CB4`
- Heading text: `#1E293B`
- Body text: `#334155`
- Main background: `#FFFFFF`
- Card / subtle background: `#F0F5FA`
- Dark accent: `#111111`
- Border / divider: `#D1D5DB`

UI style:

- Minimal
- Clean
- Based on current Elenza website branding
- White background
- Light blue-grey cards
- Blue primary buttons
- Blue active states and links
- Simple tables
- No clutter
- No heavy dashboard

## 3. Change Management Instruction

This document is the master requirement specification for the project.

Whenever a correction, change, or update is given later:

- Do not patch only one line.
- Rewrite the complete updated requirement specification.
- Keep previously approved requirements.
- Add new corrections properly.
- Remove outdated or conflicting points.
- Do not hallucinate new modules.
- Keep the system simple.

## 4. Core Principles

1. Data entry is the first priority.
2. Reports must be in a separate tab.
3. Dashboard is not required.
4. Every user must have login.
5. Every machine or station must have separate login.
6. Dropdowns must be used wherever possible.
7. Dropdowns must support typing and search.
8. Avoid unnecessary manual typing.
9. Completed orders must disappear from the current machine login.
10. Partial completed orders must remain in the current machine and also move to the next machine.
11. Rejected orders must disappear from the current machine and go back to the previous machine.
12. Dispatch stage must be included after packing.
13. Admin must be able to add or edit machines and change sequence.
14. All reports must support search, sort, and filter.
15. Code must be reviewed multiple times before final delivery.

## 5. Technology Requirements

### Frontend
- HTML
- CSS
- JavaScript only where needed

### Database
- Microsoft Access database
- Use `.accdb` format

### Backend or Connection
- Use a suitable local backend connection method for Microsoft Access.
- Keep implementation simple.
- Do not use unnecessary frameworks unless required.
- Do not convert this into a complex SaaS product.

Important direction:

- This is not a dashboard project.
- This is not an analytics-first project.
- This is a data entry and production tracking system.

## 6. User Roles

### 6.1 Super User / Admin

Can access everything.

Permissions:

- Manage users
- Manage dealers
- Manage dropdown masters
- Manage customer type master
- Manage order type master
- Manage machine or station master
- Manage production sequence
- View all orders
- Edit all data
- View all reports
- Correct wrongly updated statuses
- Access full audit trail

### 6.2 Data Entry User

Permissions:

- Add dealer data
- Add quotation basic details
- Confirm order
- Add optimisation details
- Add procurement details
- View relevant reports
- Cannot manage users
- Cannot change machine sequence

### 6.3 Machine User

Each machine or station has its own login.

Example logins:

- Hot Press Login
- Cutting Login
- Edgebanding Login
- Drilling Login
- QC Login
- Packing Login
- Dispatch Login

Permissions:

- See only orders assigned to that machine or station
- Update status as `Completed`, `Partial Completed`, or `Rejected`
- Add remarks
- Add date and time
- Cannot edit dealer, quotation, procurement, or optimisation data

### 6.4 Management User

Permissions:

- View reports
- View order status
- Search and filter data
- Cannot modify master settings unless admin gives permission

### 6.5 Production Planner User

Permissions:

- Open a separate planner workspace page after login
- View all active planning orders from confirmed stage up to packing stage
- View machine-wise grouped planning tables
- Assign priority as `High`, `Medium`, `Low`, or blank
- View current stage for every active order
- Use current stage as the single truth for planner visibility
- Export each planner table to Excel
- Export machine-wise planner data sorted by confirmation date in descending order
- Open history from planner context
- Cannot treat one order as having more than one current stage at the same time

## 7. Main Modules

1. Login
2. Dealer Data Entry
3. Quotation Data Entry
4. Order Confirmation
5. Optimisation
6. Procurement
7. Production Tracking
8. Dispatch
9. Production Planner
10. Reports
11. Masters
12. Users
13. Audit Log

## 8. Login Module

Every user must login using:

- Username
- Password

Login must identify:

- User role
- Assigned machine or station, if machine user
- Permission level

After login:

- Data entry user opens the Data Entry section
- Machine user opens assigned machine order list
- Dispatch user opens Dispatch order list
- Management user opens Reports
- Admin sees all menus

## 9. Dealer Data Entry

Fields:

- Dealer ID or auto-generated
- Dealer name
- Contact person
- Mobile number
- WhatsApp number
- Email
- Company name
- City
- Area
- GST number
- Address
- Dealer type
- Active or inactive
- Remarks

Rules:

- Dealer name must be searchable.
- Dealer dropdown must be available in quotation entry.
- Duplicate mobile or GST warning is required.
- Admin can edit dealer details.

## 10. Quotation Data Entry

Purpose: capture basic enquiry or quotation details.

Fields:

- Quotation number or auto-generated
- Quotation date
- Dealer name dropdown
- Customer name
- Customer type dropdown
- Order type dropdown
- Main order dropdown
- Sub order dropdown
- Order number
- Site or project name
- Location
- Approx value
- Expected confirmation date
- Remarks
- Created by
- Created date and time

Rules:

- Order number must be unique.
- Dropdowns must support typing and search.
- Customer type dropdown values must come from the Customer Type Master.
- Quotation should not require unnecessary fields.
- Main order and sub order must be identifiable.

## 11. Order Type Master

Order type master must include modular interior production categories.

Default order types:

- Laminate
- Carcase
- Membrane
- Veneer
- Alu Glass
- Acrylic
- PU
- Profile Shutter
- Glass Shutter
- Wardrobe
- Kitchen
- TV Unit
- Full Home
- Loose Furniture
- Other

Admin must be able to:

- Add order type
- Edit order type
- Disable order type
- Reorder list

Disabled order types should not appear in new entries.

## 12. Customer Type Master

Customer type must be maintained as a master.

Default customer types for now:

- EL
- ADM
- MCB
- BRGWF

Admin must be able to:

- Add customer type
- Edit customer type
- Disable customer type
- Reorder customer type list
- Remove unused customer type values later if business requires

Rules:

- Customer type dropdown in quotation entry must come from this master.
- Disabled customer types should not appear in new entries.
- If a customer type is already used in transactions, disable should be preferred over deletion.
- The master must support future adding or removing of values without developer help.

## 13. Order Confirmation

Purpose: mark quotation or order as confirmed.

Fields:

- Order number dropdown
- Confirmation date
- Confirmed by
- Remarks
- Status as `Confirmed`

Rules:

- Dropdown should show only not-yet-confirmed orders.
- Once confirmed, order becomes eligible for optimisation.

## 14. Order Optimisation

Purpose: record board optimisation and raw material planning.

Fields:

- Order number dropdown
- Optimisation date
- Number of boards
- RM details
- Optimisation done by
- Remarks

Rules:

- Order dropdown should show only confirmed but not optimised orders.
- Number of boards must be numeric.
- After optimisation, order becomes eligible for procurement.

## 15. Procurement

Purpose: track purchase order and material receiving.

Fields:

- Order number dropdown
- PO raised date
- PO number
- Vendor name dropdown
- Item details
- MRN date
- Procurement status
- Remarks

Procurement status values:

- PO Pending
- PO Raised
- Partial Material Received
- Material Received
- Cancelled

Rules:

- PO number should not duplicate.
- Multiple items may be added against one order.
- MRN date can be item-wise.
- Procurement completion can make order ready for production.

## 16. Production Tracking

Default production sequence:

1. Hot Press
2. Cutting
3. Edgebanding
4. Drilling
5. QC
6. Packing
7. Dispatch

Admin must be able to:

- Add new machine or station
- Rename machine or station
- Disable machine or station
- Change sequence
- Insert a machine between existing machines

Machine screen should show:

- Order number
- Dealer name
- Customer name
- Order type
- Main order
- Sub order
- Current station
- Previous station
- Next station
- Status buttons
- Remarks field
- Last updated date and time

Machine user actions:

- Completed
- Partial Completed
- Rejected

Stage display rules:

- Every order must have only one `Current Stage` at any time.
- Planner and shared planning screens must not show two active stages for the same order.
- `Visible Stations` is not required as a planner display column.

## 17. Production Movement Logic

### 17.1 Completed

When machine user marks an order as `Completed`:

- Order disappears from current machine login.
- Order moves to next machine or station.
- If current station is Dispatch, order becomes `Fully Completed / Dispatched`.
- Fully completed or dispatched orders must not appear in machine login.

Example:

Cutting -> Completed -> moves to Edgebanding

### 17.2 Partial Completed

When machine user marks an order as `Partial Completed`:

- Order remains visible in current machine.
- Same order also appears in next machine.
- Remarks are mandatory.

Example:

Cutting -> Partial Completed -> visible in Cutting and Edgebanding

Planner display rule for partial completion:

- Even if an order is internally visible to more than one station because of `Partial Completed`, planner UI must still show only one `Current Stage`.
- In partial state, the planner `Current Stage` remains the originating stage until a valid downstream movement actually updates the order.

### 17.3 Rejected

When machine user marks an order as `Rejected`:

- Order disappears from current machine.
- Order moves back to previous machine or station.
- Rejection reason is mandatory.

Example:

Edgebanding -> Rejected -> moves back to Cutting

If the first machine rejects:

- Order goes to Admin or Data Entry correction queue.

Planner reapproval rule:

- If the first active production station rejects an order, planner or correction review must happen before the order is reintroduced into production.

## 18. Dispatch Stage

Dispatch is mandatory after Packing.

Dispatch screen should show:

- Order number
- Dealer name
- Customer name
- Order type
- Packing completed date
- Dispatch date
- Vehicle or transport details
- Dispatch remarks
- Dispatch status

Dispatch status values:

- Pending Dispatch
- Partially Dispatched
- Dispatched
- Hold

Rules:

- Dispatch user must have separate login.
- Dispatch completed order should disappear from Dispatch login.
- Dispatched order should appear only in reports.
- Partial dispatch should remain visible in Dispatch.
- Dispatch remarks are mandatory for Hold or Partially Dispatched.
- Packed, Dispatch, and Dispatched rows must not appear in Planner Priority Desk.
- Dispatched orders must not appear anywhere in the production planner workspace.

Packed stage rule:

- If an order is updated as `Packed`, the one and only `Current Stage` becomes `Packed`.
- If an order is later updated as `Dispatched`, the one and only `Current Stage` becomes `Dispatched`.

## 18A. Production Planner Workspace

The system must include a separate production planner page connected to login for `planner.user` and planner-authorised admin access.

Planner workspace direction:

- Minimal UI
- Separate page from the main production entry screens
- Table-first layout
- No dashboard clutter
- Search, filter, and Excel export

Planner tabs:

1. All Orders
2. Machine Wise
3. Priority Desk

Planner visibility rules:

- Show active planning orders from confirmed stage up to packing stage
- Do not show dispatched orders
- Show only one `Current Stage`
- Do not show `Visible Stations` as a planner display field

Planner fields:

- Order Number
- Dealer
- Customer
- Order Type
- Current Stage
- Priority
- EDD

Planner priority rules:

- Priority must be assignable directly from planner tables, not only shown as text
- Priority values must be:
  - High
  - Medium
  - Low
  - Blank
- High priority rows must float above Medium, Low, and blank during planner sorting

Planner export rules:

- All Orders tab must support Excel export
- Machine Wise tab must support Excel export
- Each machine table in Machine Wise must support its own Excel export
- Priority Desk must support Excel export
- Export must respect currently filtered planner rows

## 19. Reports Tab

Reports must be separate from data entry.

All reports must have:

- Search
- Sort
- Filter
- Date range filter
- Export to Excel
- Status filter
- Dealer filter
- Order type filter
- Machine or station filter

Required reports:

1. Dealer report
2. Quotation report
3. Confirmed order report
4. Optimisation report
5. Procurement report
6. Production status report
7. Machine-wise pending report
8. Rejected order report
9. Partial completed order report
10. Completed order report
11. Dispatch report
12. Order lifecycle report

Additional reporting direction:

- Planner workspace tables themselves must support Excel export
- Order lifecycle and history must remain separate from planner assignment screens

## 20. Master Settings

Admin can manage the following masters.

### Dealer Master

- Dealer details

### Customer Type Master

- EL
- ADM
- MCB
- BRGWF

The system must allow future addition, edit, disable, reorder, or removal of customer type values from this master.

### Order Type Master

- Laminate
- Carcase
- Membrane
- Veneer
- Alu Glass
- Acrylic
- PU
- Profile Shutter
- Glass Shutter
- Wardrobe
- Kitchen
- TV Unit
- Full Home
- Loose Furniture
- Other

### Machine or Station Master

- Hot Press
- Cutting
- Edgebanding
- Drilling
- QC
- Packing
- Dispatch

### User Master

- Name
- Login ID
- Password
- Role
- Assigned machine or station
- Active or inactive

### Vendor Master

- Vendor name
- Contact
- Material category
- Remarks

## 21. UI Requirements

Pages:

- Login
- Data Entry
- Production
- Dispatch
- Reports
- Masters
- Users
- Settings

UI style:

- Minimal Elenza web-style interface
- ElenzaIndia.com logo at login and top left
- Use current `www.elenzaindia.com` colour palette
- Main background `#FFFFFF`
- Card background `#F0F5FA`
- Primary buttons `#046BD2`
- Button hover / active `#045CB4`
- Heading text `#1E293B`
- Body text `#334155`
- Border and divider `#D1D5DB`
- Simple tables
- Clear labels
- Fast form entry
- No visual clutter

Machine UI:

- Simple order list
- Three buttons: `Completed`, `Partial Completed`, `Rejected`
- Remarks box
- No dashboard cards
- No unnecessary charts

Production Planner UI:

- Separate planner page for planner user
- Show `Confirmation Date` in planner tables
- Show one `Current Stage` only
- Do not show `Visible Stations` as a display column in planner tables
- Keep internal routing logic separate from displayed planner columns
- Machine-wise tab must support export
- Machine-wise export must be sorted by confirmation date descending

## 22. Validation Rules

- Order number must be unique.
- PO number should be unique.
- Mobile number should be valid.
- Required fields must be marked clearly.
- Rejection requires reason.
- Partial completion requires remarks.
- Dispatch hold requires remarks.
- Machine user cannot update other station orders.
- Completed final orders should disappear from login.
- Dropdown values must come from master tables.
- Disabled dealers, vendors, users, customer types, and order types should not appear in new entries.

## 23. Audit Trail

Every important action must be logged.

Track:

- Created by
- Created date and time
- Updated by
- Updated date and time
- Previous status
- New status
- Remarks
- Machine or station
- User login

Production movement history and dispatch history must be visible in the order lifecycle report.

Additional history rules:

- Planner priority updates must be captured in audit trail or history
- Order lifecycle must always reflect one latest valid current stage
- Packed and dispatched stage changes must be visible in lifecycle history
- Visible station routing must not override the latest valid stage in history or planner display

## 24. Microsoft Access Database Tables

Suggested tables:

1. `tbl_users`
2. `tbl_roles`
3. `tbl_dealers`
4. `tbl_quotations`
5. `tbl_orders`
6. `tbl_order_confirmations`
7. `tbl_optimisation`
8. `tbl_procurement`
9. `tbl_procurement_items`
10. `tbl_vendors`
11. `tbl_order_types`
12. `tbl_customer_types`
13. `tbl_machines`
14. `tbl_machine_sequence`
15. `tbl_production_tracking`
16. `tbl_production_history`
17. `tbl_dispatch`
18. `tbl_audit_logs`
19. `tbl_dropdown_masters`

## 25. Order Status Flow

Quotation Created
-> Order Confirmed
-> Optimisation Done
-> Procurement Started
-> Material Received
-> Production Started
-> Hot Press
-> Cutting
-> Edgebanding
-> Drilling
-> QC
-> Packing
-> Dispatch
-> Fully Completed / Dispatched

Production statuses:

- Pending
- In Progress
- Completed
- Partial Completed
- Rejected
- Production Completed
- Pending Dispatch
- Partially Dispatched
- Dispatched
- Hold

Current stage rule:

- Planner displays must always derive one latest valid `Current Stage`
- `Packed` overrides earlier production stages until dispatch
- `Dispatched` removes the order from planner workspace
- Dispatched filtering must be based on stage or dispatch status, not on visible stations
- Visible stations may be used internally for routing or grouping, but not as the main truth for planner stage display

## 26. Code Quality Requirements

Developer must follow:

1. Clean folder structure.
2. Role-based access.
3. Microsoft Access database connection handled safely.
4. No hardcoded dropdowns.
5. No hardcoded machine sequence.
6. Proper database relationships.
7. Frontend and backend validation.
8. Secure password hashing.
9. Audit logging.
10. Reusable HTML and CSS components where possible.
11. Reusable JavaScript functions where needed.
12. Error handling for failed saves.
13. Pagination for reports.
14. Searchable dropdown component.
15. Consistent naming.
16. No unused code.
17. No duplicate logic.
18. Basic testing for production flow.
19. Manual testing before handover.

## 27. Mandatory Review Checklist

### Review 1: Functional Review

- Dealer entry works
- Quotation entry works
- Order confirmation works
- Optimisation works
- Procurement works
- Machine login works
- Dispatch works
- Completed moves order forward
- Partial keeps order in current and next station
- Rejected moves order backward
- Reports show correct data

### Review 2: Permission Review

- Admin can access all
- Data entry cannot change sequence
- Machine user sees only assigned station
- Dispatch user sees only Dispatch
- Management user sees Reports only
- Disabled users cannot login

### Review 3: Data Review

- No duplicate order numbers
- No duplicate PO numbers
- Dropdowns come from masters
- Search and filter work
- Audit logs are saved

### Review 4: UI Review

- Minimal UI
- ElenzaIndia.com logo visible
- Elenza website colour scheme visible
- No dashboard clutter
- Fast data entry
- Searchable dropdowns
- Mobile and tablet usable
- Clear buttons and status

### Review 5: Production Flow Review

- Final completed orders disappear
- Partial order appears in both current and next station
- Rejected order goes back to previous station
- Dispatch comes after packing
- Dispatch completed order disappears from Dispatch login
- First machine rejection goes to correction queue
- Sequence change does not break old records

## 28. First Version Scope

### Must Build

- Login
- User roles
- Dealer data entry
- Quotation data entry
- Order confirmation
- Optimisation entry
- Procurement entry
- Machine-wise production tracking
- Dispatch stage
- Separate production planner page
- Planner priority assignment
- Planner Excel export for each planner table
- Machine-wise planner export sorted by confirmation date descending
- Planner tables showing current stage without visible-stations display column
- Machine sequence management
- Reports tab
- Searchable dropdowns
- Audit log
- Excel export
- Microsoft Access database

### Not Required in First Version

- Dashboard
- Advanced analytics
- Accounting
- Inventory valuation
- Barcode scanning
- Mobile app
- WhatsApp automation
- Customer portal

## 29. Final Expected Output

A production-ready minimal local web application for ElenzaIndia.com modular interiors B2B production management.

The system must use HTML, CSS, JavaScript where needed, and Microsoft Access database.

The final system must be simple enough for factory staff to use daily and flexible enough for admin to manage users, machines, order types, customer types, dropdowns, sequence, production flow, dispatch, and reports without developer help.
