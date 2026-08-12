# Web performance and architecture review

Review date: 2026-07-18

This is the measured baseline and first implementation pass for `Web/`. It
separates completed work from the next architecture iterations so performance
work does not regress into one-off route fixes.

## Measurement method

- Backend endpoint measurements used the real `H:\ColorVision` artifact tree
  and a transactionally consistent temporary copy of `marketplace.db`.
- Access analytics was disabled in the benchmark process. Each endpoint was
  warmed twice and then measured for 15 requests through the Flask test client.
- Frontend sizes come from a production Vite build and its generated preload
  graph. Values are gzip sizes unless stated otherwise.
- No benchmark request rewrote the production index or analytics database.

## Results

| Endpoint | Legacy bytes | Compact bytes | Legacy median | Compact median | Compact p95 |
|----------|-------------:|--------------:|--------------:|---------------:|------------:|
| Home | 2,933,265 | 14,064 | 43.93 ms | 19.36 ms | 21.15 ms |
| Releases | 5,759,893 | 74,707 | 45.23 ms | 19.92 ms | 21.29 ms |
| Changelog | 1,165,048 | 200,460 | 41.45 ms | 6.33 ms | 10.43 ms |

The compact projections reduce response size by about 99.5%, 98.7%, and 82.8%
respectively. Home and releases no longer construct the complete legacy release
DTO before discarding most of it. Windows and Android history use independent
server pagination; a synthetic 5,000-APK catalog remains below 64 KiB per
compact response.

On 2026-08-12, the React changelog page also moved to server pagination while
the unparameterized compact response remained available for compatibility. For
the real 381-release changelog, the first 20-release JSON response fell from
156,526 to 10,110 bytes (93.5%); gzip size fell from 52,250 to 4,848 bytes.

Directory browsing now applies case-insensitive name and item-type filters
before sorting and pagination. This fixes searches that previously inspected
only the first 200 rendered entries; the largest measured public directory had
264 entries. The filesystem remains authoritative, and an indexed browse query
is still reserved for directories that grow beyond this bounded scale.

The plugin catalog response now carries the full category filter list derived
from the same indexed snapshot used for search. The React catalog therefore
loads with one request instead of immediately repeating the catalog read via
`/api/plugins/categories`; that legacy endpoint remains available to existing
clients.

Plugin detail history now uses the compact Web projection and server paging.
Against the real 182-entry ProjectARVRPro history, the first 20-entry response
fell from 130,868 to 47,763 bytes (63.5%); gzip fell from 31,145 to 16,878
bytes (45.8%). Common metadata, current versions, rendered documents, and the
first archive slice were byte-for-byte value equivalent, including archive
ordering; only unused raw Markdown and later archive pages were omitted. In an
isolated process with an empty temporary index, application import cost 252 ms
and the first fallback scan cost 5.85 s; after that readiness work, the compact
request median was 184 ms with 191 ms p95 over 15 reads.

Public initial module-preload JavaScript fell from about 681.8 KiB to 233.5 KiB
gzip (about 65.8%). Admin pages, Pro Components, traffic analytics, and DOMPurify
are route chunks instead of public-entry dependencies. The 2.34 MiB decorative
home video is attached only after window load and an idle opportunity, and is
not requested for reduced-motion or data-saving clients.

The release change signature previously took 3.7-4.5 seconds against the real
history tree. The final metadata signature has a 29.52 ms median over ten reads
(27.64 ms minimum, 131.26 ms maximum) while still detecting a same-name file
overwrite. Plugin detail GET no longer hashes packages; a measured 63.2 MiB
package hash costing about 381.9 ms moved to index refresh work.

## Completed first pass

- Added compact, bounded home, release, and changelog read models while keeping
  the legacy default DTOs unchanged.
- Made release/update/tool/plugin refreshes single-flight within a process and
  tied persisted rows to a pre-scan signature. Mid-scan changes are visible to
  the next check instead of being accepted under a newer signature.
- Included same-name historical package overwrites plus plugin README and
  changelog edits in plugin signatures.
- Moved package hashes out of GET and exposed an explicit `hashPending` state.
- Coalesced API-key `last_used_at` persistence to one write per key per minute;
  an instrumented 20-request same-minute verification performs one usage write
  instead of 20 while active/expiry/secret/scope checks still run every time.
  In a 50-verification comparison, the prior forced-write path issued 50
  updates/commits in 3.38 s versus 1 update/commit in 3.22 s; cryptographic key
  verification still dominates CPU time, while the removed cost is SQLite
  write-lock and WAL amplification.
- Bounded scheduler history to 30 days by default while preserving every job's
  latest run and all running rows. A production preflight over 56,612 runs
  identified 34,229 removable rows; a same-scale synthetic indexed delete took
  62.26 ms and left 22,383 rows.
- Routed plugin, application, update, tool, transfer, generic storage, and
  legacy downloads through one `ArtifactDeliveryService`. A two-byte partial
  plugin range previously incremented the full-download counter; completion is
  now recorded only after a full `200` body or whole-file `206` body is
  iterated, while HEAD, 304, partial ranges, and interrupted streams do not
  count. In a local in-process 32 MiB streaming comparison with one warmup per
  path and five measured passes, raw `send_file` had an 8.29 ms median versus
  7.65 ms through the unified boundary; bytes were identical and the wrapper
  did not buffer the file. This is not an end-to-end network benchmark.
- Replaced raw SQLite file copies with SQLite online backup, integrity checking,
  and atomic replacement so committed WAL content is included.
- Added bounded, batched access analytics with normalized route templates,
  coarse clients, daily rotating visitor HMACs, explicit visitor-day semantics,
  retention, recorder health, and `/admin/traffic`.
- Applied access retention to recognized database snapshots as well as the live
  database. A manual backup is scrubbed and checked before success is returned.
- Bounded NAS deployment backups to the newest 10 successful and 3 failed
  deployments after a new deployment is healthy. The production preflight found
  40 backups using 763,471,066 bytes; 28 old successful backups totaling
  525,584,018 bytes were eligible, while both failed backups and all
  unclassified directories remained protected.
- Bounded administrator audit rows to 365 days and manual database snapshots to
  the newest 10 by default. The same daily job scrubs expired audit rows from
  recognized snapshots before rotation; new backups apply both access and audit
  retention before success. A production preflight found 377 audit rows (oldest
  2026-06-20), so the default cutoff removes none, and no manual DB snapshots
  currently exist.
- Bounded NAS Git-bundle transport files to the newest 3 verified deployed
  ancestors. The production preflight found 25 bundles using 9,711,112 bytes:
  23 were valid deployed ancestors, while 2 legacy bundles lacked the required
  `HEAD` reference and remained protected. The read-only cleanup plan selected
  20 verified transport files totaling 9,690,663 bytes; current,
  unverified, divergent, unexpected, and reparse-point files are never selected.
- Audited `web-deploy-history.jsonl` at 40,765 bytes and 57 valid records, with
  no malformed rows and no recovery consumer beyond per-backup status markers.
  Deployment writes now atomically retain the newest 500 valid records, while
  `/admin/deployments` provides a sanitized, paginated operational view.
- Replaced the deployment SSH loader's EOF-dependent `ReadToEnd()` transport
  with a single-line payload contract. A production audit found five orphaned
  loader processes with no children and about 53 MB working set each; they were
  removed after exact command-line classification. The new Bundle DryRun
  returned in 7.7 seconds without adding a loader process or history record.
- Added route-level frontend splitting, request cancellation, stale-state fixes,
  changelog/plugin HTML sanitization, immutable hashed-asset caching, and lazy
  chunk recovery after rolling deployments.
- Documented dependency direction and extension ports in `ARCHITECTURE.md`.

## Next iterations

1. Replace process-local refresh locks with a SQLite lease before running more
   than one WSGI worker against the same storage and index. The 2026-08-12 NAS
   audit found one listener process, so this remains conditional rather than a
   current production defect.
2. Finish the application-factory migration: remove route dependencies on
   mutable `app.py` globals, centralize the connection factory, and move SQL into
   feature repositories.
3. Add an indexed browse query instead of sorting/scanning an entire directory,
   and throttle heartbeat write amplification.
4. Add separate, versioned SPA page-view and Web Vitals ingestion. Server HTTP
   requests, page views, downloads, sessions, and visitors must remain distinct
   metrics; trusted-proxy client identity also needs explicit configuration.
5. Add OpenAPI as the source of truth and generate TypeScript DTOs. The current
   handwritten interfaces are contract-tested but still transitional.
6. Split the remaining 506.16 KiB minified `ProForm` admin chunk if publish-page
   navigation performance becomes material; it is lazy and does not affect the
   public preload today.

## Verification snapshot

- Backend: 559 tests passed.
- Frontend: ESLint passed; production build passed (3,780 modules).
- Dependency audit: zero npm vulnerabilities.
- Whitespace check: `git diff --check -- Web` passed.

