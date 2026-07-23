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
- Replaced raw SQLite file copies with SQLite online backup, integrity checking,
  and atomic replacement so committed WAL content is included.
- Added bounded, batched access analytics with normalized route templates,
  coarse clients, daily rotating visitor HMACs, explicit visitor-day semantics,
  retention, recorder health, and `/admin/traffic`.
- Applied access retention to recognized database snapshots as well as the live
  database. A manual backup is scrubbed and checked before success is returned.
- Added route-level frontend splitting, request cancellation, stale-state fixes,
  changelog/plugin HTML sanitization, immutable hashed-asset caching, and lazy
  chunk recovery after rolling deployments.
- Documented dependency direction and extension ports in `ARCHITECTURE.md`.

## Next iterations

1. Replace process-local refresh locks with a SQLite lease before running more
   than one WSGI worker against the same storage and index.
2. Finish the application-factory migration: remove route dependencies on
   mutable `app.py` globals, centralize the connection factory, and move SQL into
   feature repositories.
3. Route every plugin/app/update/tool/transfer download through one
   completion-aware `ArtifactDeliveryService`, with correct GET/HEAD/range
   semantics and one download event contract.
4. Add an indexed browse query instead of sorting/scanning an entire directory,
   and throttle API-key `last_used` writes and heartbeat write amplification.
5. Add separate, versioned SPA page-view and Web Vitals ingestion. Server HTTP
   requests, page views, downloads, sessions, and visitors must remain distinct
   metrics; trusted-proxy client identity also needs explicit configuration.
6. Add OpenAPI as the source of truth and generate TypeScript DTOs. The current
   handwritten interfaces are contract-tested but still transitional.
7. Add retention/rotation for audit rows, job-run history, and the number of DB
   backup files. Access rows inside backups already obey analytics retention.
8. Split the remaining 506.16 KiB minified `ProForm` admin chunk if publish-page
   navigation performance becomes material; it is lazy and does not affect the
   public preload today.

## Verification snapshot

- Backend: 426 tests passed.
- Frontend: ESLint passed; production build passed (3,776 modules).
- Dependency audit: zero npm vulnerabilities.
- Whitespace check: `git diff --check -- Web` passed.

