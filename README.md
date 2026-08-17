# Elenza PMS Update Notes

Latest additions (2026-08-17):
- Production remarks request system: token-based URLs, bulk+individual remarks, replied/unreplied tracking
- Remarks report: done/pending summary, Excel export, scheduled email at 21:00 IST
- Marketing portal priority feature: badges, row highlighting, filter dropdown, report modal
- Remarks reply page at `/remarks-reply.html?token=...`
- Planner remarks tab for managing requests
- Full site backup: `backup/site-ftp-20260817-140546/`

Previous additions:
- Machine QR Scanner page at `/qr-scanner.html`
- QR Label Printer page at `/qr-printer.html`
- Priority Desk page at `/priority-desk.html`
- Packing portal fixes: station compatibility, history API, duplicate-save prevention
- 100-slide Elenza PMS pitch deck generated

Known live note:
- `api.ashx` root URL returns `404` by design.
- App uses `api.ashx?action=...` through `script.js`.
- App pool recompilation via `Timestamp.cs` upload to `App_Code/`.

Current focus:
- Keep uploads in sync with live files.
- Do not change database unless explicitly asked.
- SLA/EDD auto-calculation deferred (compilation issues on shared hosting).
