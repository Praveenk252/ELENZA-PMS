# AIMS Kitchen Designer

## Project Overview

AIMS (Automated Interior Manufacturing System) is a browser-based kitchen design, quotation, and Bill of Materials (BOM) engineering tool. It transforms raw kitchen measurements into technical drawings, accurate material lists, and client-ready quotations — all within a single HTML file, no server required.

## Vision

To empower every kitchen manufacturer and interior designer with an intelligent, offline-first platform that transforms raw measurements into manufacturable outputs — technical drawings, accurate BOMs, and client-ready quotations — in minutes, not days, bridging the gap between design intent and production reality.

## Business Goals

- Eliminate manual drafting and spreadsheet-based BOM generation
- Reduce errors in cabinet dimensions and material estimation
- Provide instant client quotations with tax, discount, and currency support
- Enable manufacturing-ready output (SVG drawings, CSV BOM, CSV quotation)
- Scale across 50+ future projects beyond kitchens

## Target Users

- Modular kitchen manufacturers
- Interior design firms
- CNC job shops
- Kitchen sales consultants
- Small to medium furniture workshops

## Problems Solved

1. **Manual drafting** — SVG elevation and plan views generated automatically from parameters
2. **BOM errors** — Parts list auto-calculated per cabinet type, dimensions derived from project params
3. **Pricing inconsistency** — Standardized pricing matrix by cabinet type and width
4. **Layout planning** — Auto-layout engine fills walls with standard cabinet widths
5. **Multi-format output** — SVG, CSV (quotation + BOM), JSON export/import

## Functional Requirements

- 4 kitchen layout types: Straight, L-Shape, U-Shape, Island
- Auto-layout engine with standard cabinet widths [300, 450, 500, 600, 750, 800, 900]
- Cabinet library: 9 base types + cornerBlind + 3 wall types + 3 tall types
- SVG plan view (top-down) for all layouts
- SVG elevation view (multi-wall sections)
- Dynamic form fields per layout type
- Cabinet schedule with wall assignment and reordering
- Quotation with tax, discount, currency (INR/USD/AED)
- BOM with part numbers, dimensions, quantities, and materials
- CSV export for quotation and BOM
- SVG export and print
- Project JSON import/export

## Non-Functional Requirements

- Offline-first: runs entirely in browser, no server needed
- Single-file deployment: no build step, no dependencies
- Instant regeneration on parameter change
- Error resilience: inline error handler catches all JS errors
- Cross-platform: works in any modern browser

## Features

**Current:**
- Layout type selector with dynamic wall inputs
- Auto-layout engine (best-fit standard widths)
- 15+ cabinet types (base, wall, tall, corner)
- Plan view (top-down SVG)
- Multi-wall elevation view
- Cabinet schedule with wall badges and reordering
- Quotation with pricing, tax, discount
- BOM with parts and materials
- Export: SVG, CSV (quotation + BOM), JSON
- Import: JSON project files
- Print support
- Handle system selection (integrated, bar, gola)
- Shelves on door cabinets
- Corner blind cabinet (1100×1100, shutter 450mm)

**Planned:**
- 50+ additional project types beyond kitchens
- CNC/DXF output
- Cut optimization
- Edge banding scheduling
- Hardware catalog
- Multi-user support
- Cloud sync
- 3D visualization

## Future Roadmap

See phases.md for detailed milestone breakdown.