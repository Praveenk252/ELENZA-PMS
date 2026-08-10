# Engineering Rules

## Authority

Follow this order:

1. Explicit user instruction
2. Master requirement specification
3. These `docs/` files
4. `PROJECT-CONTEXT.md`
5. Verified active production behavior

If code and documentation conflict, investigate before changing either. Preserve explicit requirements and update stale technical documentation.

## Mandatory production safety

- Never modify the API or database without explicit approval.
- Never upload Markdown files to production.
- Back up every live file before modifying, replacing, or deleting it.
- Use only the active Site 1 host unless the user explicitly changes the target.
- Verify the exact remote path before upload.
- Verify uploads by read-back hash and public endpoint behavior.
- Keep the two newest relevant backup sets.
- Do not expose credentials, password hashes, SMTP settings, cookies, or connection details.
- Do not test mutating endpoints with real orders unless the requested task requires that mutation.
- Prefer non-mutating authorization probes and isolated sessions.

## Source selection

- Do not select a source file based only on names such as `live`, `current`, `readback`, or `verify`.
- Read back the active production file before edits.
- Patch a copy of that read-back when local sources differ.
- Keep pristine backup and edited working copy separate.
- Record which file was uploaded and its SHA-256 hash.

## C# conventions

- Preserve ASP.NET Framework 4.8 and C# syntax supported by the host.
- Keep endpoint authorization on the server.
- Use parameterized OleDb commands for user-provided values.
- Treat Access parameter order as positional.
- Use existing helpers for parsing, serialization, audit, history, and database access.
- Keep status and role comparisons explicit and case-aware.
- When aliases are required, centralize them in a helper such as `IsPackingStationName`.
- Add audit/history records for operational state changes.
- Return neutral authentication errors.
- Do not add a new framework or package without a clear hosting-compatible need.

## Frontend conventions

- Use plain HTML, CSS, and JavaScript.
- Preserve same-origin requests and session cookies.
- Use `URLSearchParams` for existing form-encoded API posts.
- Escape dynamic HTML and attribute content.
- Disable submit controls during multi-request saves.
- Show actionable success and error messages.
- Use searchable dropdowns where the requirements call for them.
- Keep planner display based on one current stage.
- Do not expose internal visible-station routing as the planner's stage truth.

## Naming

- Database tables use `tbl_` prefixes.
- Database columns and wire payloads use `snake_case`.
- C# methods and types use `PascalCase`.
- C# local variables use `camelCase`.
- JavaScript variables and functions use `camelCase`.
- Status codes are uppercase with underscores.
- User-facing status labels use title case.

Do not casually rename machine or status concepts. `Packing` is the business station; `Packed` is normally a stage/status label, although the current live database uses `Packed` as the assigned station name and code provides compatibility.

## Database rules

- Do not edit or upload an `.accdb` file without explicit approval.
- Hash database files before declaring them duplicates.
- Preserve an intentional database copy inside each retained complete backup.
- Prefer disable over delete for referenced master values.
- Preserve order IDs, sequence profiles, station IDs, and audit history.
- Consider Access locking and shared-host permissions.
- Document any schema-readiness code that can alter tables automatically.

## Testing and verification

For code changes, run the relevant subset:

- Static search for every affected status/role/station comparison
- Public page HTTP check
- Login and authenticated `app-state`
- Endpoint authorization probe that cannot mutate data
- FTP read-back hash comparison
- Manual browser flow when user interaction is affected
- Database hash and record-count checks when database work is explicitly approved

Never claim a production flow is verified when only static code was inspected.

## Security

- Never add universal passwords, authentication bypasses, or plaintext credentials.
- Do not log request passwords, session cookies, or connection strings.
- Validate role and station on every mutating endpoint.
- Validate imported files and user-controlled fields.
- Keep sensitive configuration outside public web paths.
- Production should use non-debug compilation and non-detailed errors after active diagnostics are complete.

## Performance

- Avoid loading unbounded Access result sets.
- Filter by indexed IDs or station IDs before mapping in memory.
- Keep report and history limits explicit.
- Avoid repeated full `app-state` loads when a smaller endpoint is practical.
- Prevent duplicate submissions.
- Preserve pagination for large reports.

## Error handling and logging

- Return JSON errors with an appropriate HTTP status.
- Keep login failure messages neutral.
- Include enough server-side context for diagnosis without exposing secrets.
- Record operational changes in audit/history.
- Do not swallow errors unless a best-effort cleanup is intentionally safe.

## Documentation and session close

After every coding session:

- Update `docs/memory.md`.
- Update any affected architecture, rules, design, phase, or product document.
- Record files modified, verification, backup path, decisions, remaining risks, and whether API/database changed.
- Never copy secrets from `PROJECT-CONTEXT.md` into `docs/`.

## Git and file hygiene

This workspace is not currently a clean source repository. Until it is consolidated:

- Do not bulk-format monolithic files.
- Do not delete historical files merely because they look duplicated.
- Do not commit databases, cookies, temporary readbacks, or credentials to a future repository.
- Use logical commits if Git is introduced.
- Keep `work/` disposable and `backups/` recoverable.

## AI must never

- Deploy to the paused host without explicit instruction.
- Modify API/database beyond the approved scope.
- Upload `.md` files.
- Invent modules or roadmap commitments.
- Treat `Visible Stations` as the planner's current stage.
- Reapply rolled-back fixes without a new request.
- Remove `App_Code/PmsApiHandler.cs`; `api.aspx` depends on it.
- Reveal private hosting or application credentials.

