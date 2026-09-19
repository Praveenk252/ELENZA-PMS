# Architecture

## Tech Stack

| Layer | Technology | Rationale |
|---|---|---|
| **Language** | Vanilla JavaScript (ES6+) | Zero dependencies, runs in any browser |
| **Markup** | HTML5 | Single-file deployment |
| **Styling** | CSS3 with CSS Variables | Themeable, no preprocessor needed |
| **Drawing** | SVG (inline, programmatic) | Vector output, scalable, printable |
| **Storage** | JSON export/import | Portable, human-readable |
| **Persistence** | None (planned: IndexedDB) | Offline-first future |

## Project Structure

```
KitchenDesigner/
├── index.html          # Single-file application (~700 lines)
└── docs/
    ├── project.md      # Product overview & vision
    ├── architecture.md # This file
    ├── rules.md        # Engineering standards
    ├── design.md       # Design language
    ├── phases.md       # Milestone breakdown
    └── memory.md       # Engineering memory
```

## File Anatomy (index.html)

The file is organized into three sequential blocks:

### 1. CSS (`<style>`, ~80 lines)
- CSS custom properties (variables) for theming
- Layout grid for Designer panel + workspace
- Panel/card components
- Tab navigation
- Form elements
- Report tables (quotation, BOM)
- Print media queries
- Wall tag badges (A, B, C, Island)

### 2. HTML (`<body>`, ~80 lines)
- Header with title + action buttons (Print, Export SVG, Export JSON, Import)
- Nav tabs: Designer, Rules, Quotation, BOM
- Designer tab:
  - Left sidebar: Layout panel, Project panel, Add Cabinet panel, Cabinet Schedule
  - Right workspace: Plan view SVG + Elevation view SVG (stacked vertically)
- Rules tab: hardcoded rule table
- Quotation tab: tax/discount inputs + line items table + totals
- BOM tab: parts table with CSV export

### 3. JavaScript (`<script>`, ~600 lines)

#### Constants & State
```
STANDARD_WIDTHS = [300, 450, 500, 600, 750, 800, 900]
CORNER_SIZE = 1100
labels = { drawer3, drawer4, drawer2, hob, sink, doubleDoor, singleDoor, pullout, oven,
           cornerBlind, wallSingle, wallDouble, wallOpen, tallSingle, tallDouble, tallOven }
cabinets = [{ type, width, wall }]
layoutType = 'straight' | 'L' | 'U' | 'island'
```

#### SVG Primitives
```
L(x1,y1,x2,y2,w,d)    — line
R(x,y,w,h,sw,fill,rx)  — rectangle
T(x,y,text,size,anchor,weight,rot) — text
C(x,y,r)              — circle
dimH(x1,x2,y,label)   — horizontal dimension line
dimV(x,y1,y2,label)   — vertical dimension line
```

#### Core Functions

| Function | Purpose | Output |
|---|---|---|
| `p()` | Read all project params from DOM | Object |
| `bestFit(target)` | Find best combo of standard widths | Array of widths |
| `autoLayoutAll()` | Generate cabinets for all walls | Populates `cabinets[]` |
| `drawPlan()` | Top-down SVG view | Fills `#planSheet` |
| `drawElevation()` | Multi-wall elevation SVG | Fills `#sheet` |
| `drawCab()` | Single base cabinet elevation | SVG string |
| `drawWallCab()` | Single wall cabinet elevation | SVG string |
| `drawTallCab()` | Single tall unit elevation | SVG string |
| `hob()` / `sink()` | Appliance symbols | SVG string |
| `brows()` | Generate BOM rows | Array of parts |
| `bom()` | Render BOM table | Fills `#bomReport` |
| `qrows()` | Generate quotation rows | Array of line items |
| `quotation()` | Render quotation | Fills `#quotationReport` |
| `list()` | Render cabinet schedule | Fills `#cabinetList` |
| `render()` | Full redraw | Calls list+draw+quotation+bom |
| `dl()` | File download helper | Triggers browser download |
| `csv()` | CSV string builder | String |
| `updateLayoutFields()` | Show/hide wall inputs | DOM changes |
| `updateWallSelector()` | Update Add Cabinet wall dropdown | DOM changes |

#### Data Flow
```
User Input (DOM) → p() → cabinets[] 
→ autoLayoutAll() → cabinets[] (generated)
→ render() 
  → list()      (Cabinet Schedule UI)
  → drawPlan()  (#planSheet SVG)
  → drawElevation() (#sheet SVG)
  → quotation() (#quotationReport)
  → bom()       (#bomReport)
```

#### Cabinet Object
```js
{
  type: 'drawer3',       // Cabinet type key
  width: 600,            // Width in mm
  wall: 'A'             // Wall assignment: 'A', 'B', 'C', 'island'
}
```

The `wall` property connects each cabinet to its wall section in the plan and elevation views.

## Event System

- Input `oninput` → `render()` (live preview)
- Button `onclick` → specific actions (add, remove, reorder, auto-layout)
- Nav button `onclick` → tab switching (CSS class toggle)
- Window error handler catches all uncaught exceptions

## Scaling Strategy

The architecture is designed for future growth:
1. **Phase 1**: Single-file kitchen designer (current)
2. **Phase 2**: Split into index.html + engine.js + draw.js for maintainability
3. **Phase 3**: Add IndexedDB for project persistence
4. **Phase 4**: Add REST API backend for multi-user / cloud sync
5. **Phase 5**: Convert to framework (React/Svelte) for complex UI

Each phase preserves backward compatibility.