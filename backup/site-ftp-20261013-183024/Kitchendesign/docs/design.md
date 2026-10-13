# Design Language

## Colour Palette

| Token | Hex | Usage |
|---|---|---|
| `--bg` | `#edf1f4` | Page background |
| `--panel` | `#fff` | Card/panel background |
| `--ink` | `#131a22` | Primary text |
| `--muted` | `#687383` | Secondary/label text |
| `--line` | `#cbd3dc` | Borders and dividers |
| `--accent` | `#233a56` | Primary buttons, active states |
| `--danger` | `#9d2c2c` | Delete/destructive actions |

### Wall Tag Colours

| Wall | Background | Text |
|---|---|---|
| A | `#dbeafe` | `#1e40af` |
| B | `#fef3c7` | `#92400e` |
| C | `#d1fae5` | `#065f46` |
| Island | `#e9d5ff` | `#6b21a8` |

## Typography

- **Primary font**: Inter (system fallback: Arial, sans-serif)
- **Header**: 22px bold
- **Panel headings**: 14px bold
- **Body text**: 13px regular
- **Labels**: 11px bold uppercase
- **SVG text**: Arial, various sizes (10-22px)

## Spacing

- **Panel padding**: 12px 14px (headings), 12px (content)
- **Grid gap**: 10px (form fields)
- **Tab gap**: 5px
- **Layout sidebar width**: 330px
- **Toolbar padding**: 10px
- **Sheet padding**: 14px

## Components

### Buttons
- Border radius: 7px
- Padding: 8px 11px
- Font size: 13px
- Default: white bg, grey border
- Primary: accent bg, white text
- Danger: red text

### Panels
- Border radius: 10px
- Border: 1px solid `--line`
- White background
- Header with bottom border

### Form Inputs
- Border radius: 7px
- Border: 1px solid `#c7d0da`
- Padding: 8px
- Full-width within grid cell

### Tables (Quotation & BOM)
- Border collapse
- 13px font
- 6px 8px cell padding
- Header row: light grey background
- Totals row: bold

### SVG Drawings
- White background
- Box shadow: `0 2px 8px rgba(0,0,0,.12)`
- Full-width responsive
- No backgrounds on SVG elements (transparent within SVG)
- Cabinets: white fill, black stroke
- Dimensions: black lines, centered text
- Appliance symbols: hob circles, sink path

## Cabinet Schedule

Grid layout with columns: number, type, width, action buttons
- Compact: 12px font, 6px 8px padding
- Reorder buttons (↑↓) and delete (×)
- Wall tag badge before cabinet name

## Responsive Behaviour

- Minimum width: 1100px (complex layout requires horizontal space)
- SVG canvases scale to fill container width
- Print: hides nav, actions, toolbar; shows drawing full-width
- No mobile optimization (target: desktop/workstation)

## Accessibility

- Semantic HTML elements (header, nav, main, section)
- Labels associated with inputs via `for`/`id` matching
- Buttons have text labels (no icon-only buttons)
- SVG text uses Arial sans-serif for readability
- Colour contrast: all text meets WCAG AA minimum

## Future Design Considerations

- Dark mode support via CSS variable swapping
- Responsive layout for tablet usage
- Loading states for IndexedDB operations
- Toast notifications for actions
- Drag-and-drop cabinet reordering