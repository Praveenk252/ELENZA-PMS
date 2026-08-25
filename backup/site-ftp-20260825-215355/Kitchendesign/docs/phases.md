# Implementation Phases

## Phase 1 — Core Kitchen Engine (CURRENT) ~24hr

Status: **Documentation complete, ready to implement**

### Goals
- 4 kitchen layout types (Straight, L-Shape, U-Shape, Island)
- Auto-layout engine with standard cabinet widths
- Plan view + multi-elevation SVG
- Expanded cabinet library (base + wall + tall + corner)
- Updated BOM and quotation

### Tasks
1. Layout selector + dynamic wall fields (~2hr)
2. Auto-layout engine (bestFit algorithm) (~5hr)
3. Cabinet library: cornerBlind, wall types, tall types (~3hr)
4. Plan view SVG (all 4 layouts) (~6hr)
5. Multi-elevation SVG (stacked below plan) (~5hr)
6. BOM + Quotation updates (~3hr)

### Deliverable
Single `index.html` with all features working.

---

## Phase 2 — Data Persistence (NEXT) ~16hr

### Goals
- Save/load projects in browser (IndexedDB)
- Project list/dashboard UI
- Auto-save on changes

### Tasks
1. IndexedDB setup and CRUD operations
2. Project list panel with search/filter
3. Auto-save debounced on render
4. Template storage for standard layouts

### Deliverable
Projects survive page refresh, multiple projects manageable.

---

## Phase 3 — Manufacturing Output ~20hr

### Goals
- CNC-ready DXF/DWG export
- Cut optimization with waste calculation
- Edge banding schedule
- Hardware fittings catalog

### Tasks
1. DXF path generation from cabinet parts
2. Cut optimization algorithm (nested rectangles)
3. Edge banding length calculation per part
4. Hardware database (hinges, runners, screws, shelf pins)
5. Detailed shop drawing with drilling patterns

### Deliverable
Output files ready for workshop machinery.

---

## Phase 4 — Business Operations ~24hr

### Goals
- Multi-user with roles
- Customer database
- Quote history and versioning
- Order management workflow

### Tasks
1. Lightweight REST API (Node.js + SQLite or Supabase)
2. User authentication (email/password or OAuth)
3. Customer CRUD + project linking
4. Quote version history (snapshot on save)
5. Order pipeline: draft → quote → approved → production → delivered
6. Purchase order generation from BOM

### Deliverable
Web application with persistent data and multi-user support.

---

## Phase 5 — Advanced Design ~30hr

### Goals
- 3D visualization (Three.js)
- Room environment (walls, floor, lighting)
- Material/texture library
- VR walkthrough

### Tasks
1. Three.js integration for 3D kitchen rendering
2. Room builder (walls, floor, ceiling, windows)
3. Material swatch catalog with texture mapping
4. Real-time camera controls (orbit, zoom)
5. WebXR VR mode for client walkthrough

### Deliverable
Photorealistic 3D kitchen preview alongside technical drawings.

---

## Phase 6 — Scale & Generalize ~40hr

### Goals
- Support 50+ project types beyond kitchens
- Cloud sync and collaboration
- Mobile app for on-site measurement
- AI-powered layout suggestions
- B2B dealer portal

### Tasks
1. Generic room layout engine (not just kitchens)
2. Cloud sync via WebSocket/CouchDB
3. React Native mobile app for measurements + photo capture
4. AI/ML auto-layout from room dimensions and user preferences
5. Machine API integration (Biesse, Homag, SCM)
6. Dealer portal with ordering pipeline

### Deliverable
Enterprise platform for interior manufacturing.