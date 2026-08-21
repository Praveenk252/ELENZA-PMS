# Design System

## Direction

Elenza PMS uses a minimal, operational, table-first interface. Fast entry and clear status are more important than decorative dashboards.

## Brand

- Brand name: ElenzaIndia.com
- Use the approved Elenza logo at login and top-left workspace positions.
- Preserve logo proportions and legibility.
- Do not invent alternate logos or unrelated visual themes.

## Core palette

| Purpose | Color |
|---|---|
| Primary blue | `#046BD2` |
| Hover/active blue | `#045CB4` |
| Heading text | `#1E293B` |
| Body text | `#334155` |
| Main background | `#FFFFFF` |
| Subtle/card background | `#F0F5FA` |
| Dark accent | `#111111` |
| Border/divider | `#D1D5DB` |

Recent standalone planner and packing portals use a compatible navy/blue workspace treatment. Maintain visual consistency with those deployed pages when editing them.

## Typography

- Prefer the existing page font before introducing another font.
- Standalone portals currently use Plus Jakarta Sans through Google Fonts.
- Use clear hierarchy and compact operational labels.
- Avoid small text below practical factory-floor readability.

Recommended hierarchy:

- Page title: 24–34 px, bold
- Card title: 18–24 px, bold
- Body/form text: 14–16 px
- Table text: 11–13 px where density requires it
- Helper/meta text: 12–13 px

## Spacing and shape

- Use consistent 8 px-based spacing where compatible with the current page.
- Keep form controls at least 44–48 px high on touch layouts.
- Use moderate rounded corners for cards and controls.
- Use whitespace to separate tasks, not dashboard tiles.
- Avoid excessive shadows or ornamental gradients.

## Components

### Buttons

- Primary actions: blue background, white text
- Secondary actions: light blue/gray background
- Destructive actions: clearly differentiated red treatment
- Disable buttons during saves
- Keep action labels explicit, such as `Update Status`, `Export`, or `Refresh Data`

### Forms

- Place labels close to inputs.
- Mark required fields.
- Prefer dropdowns and searchable datalists.
- Use numeric input constraints for board, box, and balance values.
- Show context for the selected order before saving.
- Preserve keyboard-friendly entry.

### Cards

- Use cards to group one operational task.
- Avoid KPI/dashboard cards unless explicitly requested.
- Keep headings, counts, and actions aligned.

### Tables

- Use clear headers and light dividers.
- Support horizontal scrolling on narrow screens.
- Keep headers visible when long tables scroll.
- Allow wrapping only for fields such as order number, customer, and remarks.
- Provide search, sort, filter, and export where required.
- Empty states must explain what is absent.

### Messages

- Success: green text/background
- Error: red text/background
- Keep wording actionable.
- Do not show raw server exceptions or sensitive data.

## Planner-specific rules

- Display exactly one `Current Stage`.
- Do not display `Visible Stations` as the stage.
- High priority sorts above Medium, Low, and blank.
- Machine-wise exports use confirmation date descending.
- Filters shown in the UI must apply to exports.
- Dispatched orders must not appear.

## Machine and packing rules

- Machine screens emphasize the order list, status actions, and remarks.
- Packing shows searchable order, packed boxes, balance, update status, and persistent history.
- Completed packing orders leave the active queue but remain in history.
- Use the session's assigned station while supporting the current `Packing`/`Packed` compatibility.

## Station workflow rules

- The station page (`stations.html`) shows one station at a time; machine users land on their assigned station.
- Tabs: Update Status, Orders at Station, History.
- Packed/Dispatched orders must never appear in station lists.
- Drilling and Drilling 2 display as a single "Drilling" station everywhere (user-facing label normalization).
- Ready-order lists show only orders whose previous stations are complete; show count badge next to tab label.
- Status actions use the same Completed / Partial / Rejected semantics as the legacy scanner, with mandatory remarks for Partial/Rejected.

## Responsive behavior

- Desktop: two-column label/control forms are acceptable.
- Tablet/mobile: collapse forms to one column.
- Action buttons should become full-width where helpful.
- Tables may scroll horizontally rather than compressing into unreadable cells.
- Keep touch targets at least 44 px.

## Accessibility

- Use semantic labels, headings, buttons, and tables.
- Preserve visible keyboard focus.
- Do not rely on color alone for status.
- Maintain readable contrast.
- Provide meaningful image alt text.
- Associate error messages with the relevant task.

## Motion

Use little or no animation. If introduced:

- Keep transitions short and functional.
- Respect reduced-motion preferences.
- Never delay data entry or status confirmation.

## Dark mode

Dark mode is not currently required or consistently implemented. Do not add it unless explicitly requested and designed across all operational pages.

