# Elenza Production Management System

## Overview

Elenza PMS is a minimal, data-entry-first production management system for ElenzaIndia.com's modular-interiors B2B operations. It tracks an order from quotation through confirmation, optimisation, procurement, machine production, packing, dispatch, reporting, and audit history.

The master functional specification remains:

`ElenzaIndia-Production-Management-Requirement-Specification.md`

This document summarizes the implemented product and must not replace explicit requirements in the master specification.

## Vision

Provide factory and office teams with one simple operational system that:

- Minimizes manual typing and duplicate entry
- Shows each order's latest valid production stage
- Enforces role and station permissions
- Moves work predictably between machines
- Preserves an auditable order lifecycle
- Produces searchable, filterable, exportable operational reports

## Business goals

- Speed up quotation-to-production data entry.
- Reduce uncertainty about an order's current location.
- Make machine responsibility and pending work visible.
- Preserve traceability for status corrections and rejections.
- Support daily factory operations without a complex SaaS stack.
- Allow administrators to maintain users, masters, machines, and sequences without developer assistance.

## Target users

- **Admin:** full access, correction, configuration, and audit responsibility
- **Data Entry and Quotation users:** dealers, quotations, confirmations, and relevant reports
- **Marketing users:** scoped quotation and dealer activity
- **Optimisation users:** board optimisation and raw-material details
- **Procurement users:** purchase orders and material receipt
- **Production Planner users:** active planning, priority, machine assignment, resequencing, history, and export
- **Machine users:** assigned-station queue and production actions
- **Packing users:** packing counts, balances, completion, and packing history
- **Dispatch users:** dispatch readiness, box state, transport details, and dispatch completion
- **Management users:** read-only reporting and lifecycle visibility

## Problems solved

- Orders previously lacked one consistently visible current stage.
- Production handoffs could be difficult to trace.
- Partial completion and rejection require special routing.
- Packing and dispatch require separate operational queues.
- Different users need sharply limited access.
- Managers need reports without exposing editing controls.

## Functional scope

Implemented or represented in the current application:

- Session login and role-based sections
- Dealer and quotation management
- Order confirmation
- Optimisation
- Procurement
- Configurable production sequences
- Planner priority, movement, station assignment, and reapproval
- Machine production actions: completed, partial completed, and rejected
- Packing box quantities, balance quantities, completion, and history
- Dispatch actions, boxes, balance, and transport information
- Customer type, order type, vendor, dropdown, machine, sequence, and user masters
- Reports, lifecycle history, audit data, and Excel-compatible exports
- Scheduled and manual email-report support

## Core business rules

- An order has one displayed `Current Stage`.
- Internal visibility may include more than one station after partial completion, but planner display remains singular.
- Completion moves work forward and hides it from the completed station.
- Partial completion keeps work visible at the current and next station; remarks are required.
- Rejection moves work backward; rejection reason is required.
- Packing precedes dispatch.
- Dispatched orders do not appear in production-planner workspaces.
- Machine users can update only their assigned station.
- Disabled master values must not appear in new transactions.
- Important changes must be auditable.

## Non-functional requirements

- Fast, table-first interfaces with minimal visual clutter
- Desktop, tablet, and practical mobile usability
- Microsoft Access `.accdb` persistence
- ASP.NET Framework 4.8 hosting compatibility
- Role and station authorization on the server, not only in the browser
- Safe error responses and save validation
- Backups and rollback points before live changes
- Search, filter, sort, pagination where needed, and Excel export
- No unnecessary frontend or backend framework

## Out of scope for the first version

- Dashboard-first analytics
- Accounting and inventory valuation
- Barcode scanning
- Native mobile application
- WhatsApp automation
- Customer self-service portal

## Current production status

The active production deployment is Site 1 at the public URL recorded in `PROJECT-CONTEXT.md`. The older MyASP deployment is paused and must not be used unless the user explicitly changes the target.

Recent live work includes:

- Planner stage mapping and exports
- Packing portal authorization for the live `Packed` station label
- Packing save compatibility without changing the database
- Machine-user packing history
- Duplicate-submit protection on packing saves
- QR Label Printer page (`qr-printer.html`) for Roll/Sheet QR code label printing
- 100-slide Elenza PMS pitch deck generated (`ElenzaPMS_PitchDeck.pptx`)
- Packing portal station checks: both "Packing" and "Packed" accepted
- Machine User history visibility fixed (station filter too restrictive)
- History API enhanced: customer_name, confirmation_date, packed_boxes, balance_boxes
- Planning row dropdown excludes fully packed orders (box_count > 0 AND balance = 0)

## Roadmap

1. Consolidate the mixed support workspace into a canonical source tree.
2. Synchronize the latest live API and `App_Code` files into version-controlled local sources.
3. Add repeatable automated tests for authentication, station authorization, and production movement.
4. Remove legacy snapshots only after verified retention and rollback policy.
5. Resolve documented history-query debt across all roles.
6. Harden production configuration by disabling detailed errors and debug compilation after diagnostics are complete.

