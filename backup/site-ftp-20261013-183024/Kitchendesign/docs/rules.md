# Engineering Rules

## Coding Style

- **Language**: Vanilla ES6+ JavaScript (no TypeScript, no transpilers)
- **Formatting**: Single file, organized into clear sections with comments
- **Variable naming**: camelCase for variables and functions, UPPER_CASE for constants
- **DOM access**: `$()` shorthand for `document.getElementById()`
- **Event handlers**: Inline `onclick` / `oninput` assignments (simple SPA pattern)
- **SVG generation**: String concatenation with template literals (no DOM SVG API)

## Naming Conventions

| Category | Convention | Example |
|---|---|---|
| Constants | UPPER_SNAKE_CASE | `STANDARD_WIDTHS`, `CORNER_SIZE` |
| Functions | camelCase | `bestFit()`, `autoLayoutAll()` |
| DOM IDs | camelCase | `cabinetType`, `layoutType` |
| Cabinet types | camelCase | `drawer3`, `doubleDoor`, `cornerBlind` |
| Wall labels | Single uppercase | `A`, `B`, `C`, `island` |
| Event handlers | Descriptive | `addCabinetBtn.onclick` |

## File Organization

Rules for the single `index.html`:
1. CSS first (in `<style>` in `<head>`)
2. HTML structure second (in `<body>`)
3. JavaScript last (in `<script>` at bottom of `<body>`)
4. JavaScript organized in sections:
   - Constants & State
   - SVG Primitives
   - Core Functions
   - UI Management
   - Event Handlers
   - Initialization

## Cabinet Type Naming

- Base cabinets: `drawer3`, `drawer4`, `drawer2`, `hob`, `sink`, `doubleDoor`, `singleDoor`, `pullout`, `oven`, `cornerBlind`
- Wall cabinets: `wallSingle`, `wallDouble`, `wallOpen`
- Tall units: `tallSingle`, `tallDouble`, `tallOven`

## Cabinet Dimensions

All dimensions in millimeters. Standard widths: 300, 450, 500, 600, 750, 800, 900.

## Error Handling

- Global `window.addEventListener('error')` catches all uncaught exceptions
- Error displayed as a red overlay box at the bottom of the screen
- Try/catch wrapping around entire JavaScript block
- No silent failures — all errors are visible during development

## Performance Rules

- No external dependencies (zero network requests)
- SVG regeneration on every render (acceptable for < 100 cabinets)
- CSS transitions only for tab switching (no animations)
- No setTimeout/requestAnimationFrame — synchronous rendering

## Security Rules

- No server communication (fully offline)
- No user data storage (planned IndexedDB)
- JSON import validates structure before loading
- SVG export uses `innerHTML`, safe since no user HTML input
- All text content escaped via `esc()` function to prevent XSS

## What NOT to Do

- Never add external dependencies (npm, CDN, frameworks)
- Never use class components (no OOP patterns)
- Never use localStorage for project data without user consent
- Never send data to any server
- Never add analytics or tracking
- Never modify the file structure without updating docs
- Never remove the error handler
- Never hardcode dimensions that should be computed from project params

## Git Conventions

- Single file application → commit at feature-complete milestones
- Documentation changes committed alongside code changes
- No binary files in repository
- Descriptive commit messages referencing the feature phase