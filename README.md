# Elenza PMS Update Notes

Latest additions (2026-08-21):
- Station-based workflow page at `/stations.html` — per-station workspace (Update Status / Orders at Station / History)
- Drilling 2 merged into Drilling (same physical station; users `mxdr` and `mndr` both write to "Drilling")
- Fixed QC crash in `station-ready-orders` (nonexistent `drilling2_date` column removed from mapping)
- Packed/Dispatched orders filtered out of all station dropdowns and ready lists
- QR Scanner quick-links replaced with Stations links on all 10 pages
- Planner Machine Wise grouping now uses each order's highest-sequence machine
- Full site backup: `backup/site-ftp-20260821-142516.zip`

Previous additions (2026-08-17):
- Production remarks request system: token-based URLs, bulk+individual remarks, replied/unreplied tracking
- Remarks report: done/pending summary, Excel export, scheduled email at 21:00 IST
- Marketing portal priority feature: badges, row highlighting, filter dropdown, report modal
- Remarks reply page at `/remarks-reply.html?token=...`
- Planner remarks tab for managing requests

Earlier additions:
- Machine QR Scanner page at `/qr-scanner.html` (still deployed but no longer linked)
- QR Label Printer page at `/qr-printer.html`
- Priority Desk page at `/priority-desk.html`
- Packing portal fixes: station compatibility, history API, duplicate-save prevention
- 100-slide Elenza PMS pitch deck generated

Known notes:
- `api.ashx` is the root API URL; pages call `api.ashx?action=...`.
- App pool recompilation via `Timestamp.cs` upload to `App_Code/`.
- Station users: `ct`, `hp`, `eb1`, `mxdr`, `mndr`, `qc1` — all verified working.
- `station-ready-orders` low counts (Cutting 2, Edgebanding 1, Drilling 0) are a data issue: legacy orders lack station date columns.

Current focus:
- Keep uploads in sync with live files.
- Do not change database unless explicitly asked.
- Possible future fix for station-ready-orders data coverage (migration or visible_stations approach).
