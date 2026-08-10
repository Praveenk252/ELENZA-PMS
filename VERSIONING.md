# Live Versioning

Lightweight rollback system for the live PMS app.

## Create snapshot

```powershell
.\snapshot-live.ps1
```

Optional label:

```powershell
.\snapshot-live.ps1 -Label "before-packing-change"
```

This creates:
- `backups/live-snapshot-YYYYMMDD-HHMMSS[-label]/`
- `backups/deploy-log.jsonl`

## Revert snapshot

```powershell
.\revert-live.ps1 -SnapshotPath .\backups\live-snapshot-YYYYMMDD-HHMMSS
```

The revert script:
- puts the `/PMS` app briefly offline
- uploads the snapshot back to live
- removes the offline file
- appends a revert entry into `backups/deploy-log.jsonl`

## Notes

- live root used by the PMS app is `/site1` (Site 1 active target)
- snapshots include app files and the live Access database
- always create a snapshot before risky live changes
- QR Printer page is a standalone file; snapshot it separately if deploying changes
