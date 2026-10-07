# Engineering Memory

## Session: 2026-07-24 (Morning)

### Project Init

Created the AIMS Kitchen Designer project documentation and project structure.

### Source File (Initial)

`index.html` — Single-file AIMS Technical Demo (base cabinets only, straight layout)

---

## Session: 2026-07-24 (Afternoon) — Phase 1 Implementation

### What Changed

Complete rewrite of `index.html` with all Phase 1 features:

1. **Layout type selector** — Straight, L-Shape, U-Shape, Island with dynamic wall fields (B/C/Island fields show/hide based on selection)
2. **Auto-layout engine** — `bestFit()` greedy algorithm using standard widths [300,450,500,600,750,800,900]; `autoLayoutAll()` generates cabinets per wall, inserts cornerBlind for L/U shapes
3. **Expanded cabinet library** — 16 types:
   - Base: drawer3, drawer4, drawer2, hob, sink, doubleDoor, singleDoor, pullout, oven, cornerBlind
   - Wall: wallSingle, wallDouble, wallOpen
   - Tall: tallSingle, tallDouble, tallOven
4. **Plan view SVG** — Top-down view for all 4 layouts:
   - Straight: single row along wall
   - L-Shape: two perpendicular walls with corner junction
   - U-Shape: three walls forming a U
   - Island: wall A cabinets + freestanding island
5. **Multi-wall elevation SVG** — Each wall section stacked vertically below plan view, with wall tag coloring
6. **Section view** — Typical base cabinet cross-section at bottom
7. **Fixed BOM bugs** (from memory.md):
   - Side panel length: computed from counterHeight - plinthHeight
   - Back panel length: computed
   - Door shutter height: proper height for each cabinet type
   - Added hinges for all door cabinets
   - Added drawer box parts (sides, back, bottom)
   - Top rail qty: fixed to 1
   - Added countertop segments to BOM
   - Added plinth/kickboard to BOM
   - Added shelves for door cabinets
   - Added edge banding tape
   - Added adjustable legs
8. **Updated quotation** — All 16 cabinet types priced; wall/tall categories get multiplier
9. **Wall tag badges** — Colored tags (A=blue, B=amber, C=green, island=purple) in cabinet schedule

### Key Technical Details

- SVG viewBox: 1600x1350
- Plan view: y=0-570 (plan drawing + specs + cabinet composition list)
- Elevation view: y=580+ (multi-wall sections + section view)
- Cabinets now have `wall` property: `{type, width, wall}`
- `p()` returns all project params including layoutType, wallA/B/C, island dimensions
- Dynamic wall selector in Add Cabinet panel updates based on layout type
- Auto-layout inserts cornerBlind (1100mm) at wall A start for L/U layouts
- Wall cabinets drawn shorter (90px) than base (140px) in elevation
- Tall units drawn taller (220px) in elevation, shifted up
- Hob/sink symbols drawn on top of cabinets in elevation

### File Size

- `index.html`: 123 lines (vs 43 lines original), fully minified
- Same single-file format, zero dependencies

### Notes for Next Session

- Test with real-world kitchen dimensions
- Phase 2: IndexedDB persistence
- Fine-tune plan view layout for L/U shapes (corner positioning)
- Consider adding drag-and-drop cabinet reordering