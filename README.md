# Elenza PMS Update Notes

Latest additions:
- Machine QR Scanner page deployed to Site 1 at `/qr-scanner.html`.
- Single-page scanner with tabs for Hot Press, Cutting, Edgebanding, Drilling, QC.
- External QR scanner reads order stickers → auto-prompt action buttons.
- QR Label Printer page deployed at `/qr-printer.html`.
- Priority Desk page deployed at `/priority-desk.html`.
- 100-slide Elenza PMS pitch deck generated (`ElenzaPMS_PitchDeck.pptx`).
- Packing portal fixes: server-side station checks, history API with customer/date/qty, Machine User history visibility.
- Planning row dropdown excludes fully packed orders.
- Old backups cleaned up; only 2 most recent retained.

Known live note:
- `api.ashx` root URL returns `404` by design.
- App uses `api.ashx?action=...` through `script.js`.
- Live API currently returns 500 for history-state/app-state — requires redeploy of PmsApiHandler.cs.

Current focus:
- Keep uploads in sync with live files.
- Do not change database unless explicitly asked.
- Redeploy packing fixes to Site 1.
