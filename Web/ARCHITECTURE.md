# Web architecture and evolution guardrails

This document records the intended boundary for the ColorVision web product.
It is an incremental target: existing public URLs and filesystem-backed release
contracts remain compatible while code moves toward these boundaries.

## Dependency direction

```text
app_setup / composition
  -> routes (HTTP DTOs, authentication declarations, status codes)
  -> services (use cases and domain policies)
  -> ports (small Protocol interfaces)
  -> adapters (SQLite repositories, filesystem storage, event sinks)
```

- `app_setup.py` may assemble every layer. Routes and services must not import
  `app.py` or depend on mutable module globals in new code.
- Routes do not issue SQL or scan the filesystem. They validate HTTP input,
  call one service boundary, and serialize the result.
- Services do not read Flask `request`, `session`, or `g`; request identity and
  timing data are passed explicitly.
- The artifact filesystem remains authoritative. SQLite is a read model,
  cache, operational store, and analytics store; it is not a replacement for
  release artifacts on disk.
- Database migrations own schema evolution. Repositories own SQL. Cache,
  audit, analytics, and artifact indexes are separate responsibilities even
  when they share one SQLite file.

## Product modules

New work should converge on feature-owned modules instead of expanding the
Backend root or the frontend page folder indefinitely.

```text
Backend/
  routes/
  services/
    analytics/
    marketplace/
    releases/
    storage/
  db/
    migrations/
    repositories/

Frontend/src/
  app/                 # router and providers
  features/
    admin/analytics/
    marketplace/
    releases/
    storage/
  shared/
    api/
    lib/
    ui/
```

Moves from legacy module paths should be mechanical and staged. Keep a thin
re-export shim when tests, scripts, or external consumers still import an old
path; remove the shim only after repository-wide and release-contract checks.

## Extension ports

Future capabilities should attach through small interfaces with one clear
owner. The first implementations may remain local filesystem or SQLite based.

- `ArtifactStore`: list metadata, open a read stream, stage a write, and
  atomically replace an artifact.
- `ArtifactCatalogReader` / `ArtifactIndexRepository`: read and refresh the
  SQLite artifact read model without exposing SQL to routes.
- `ArtifactDeliveryService`: authorize and stream plugin, application, update,
  tool, and transfer downloads through one completion-aware boundary.
- `EventPublisher`: publish statically registered domain events such as
  `artifact.published`, `artifact.downloaded`, and `storage.changed`.
- `StorageChangeHandler`: update the affected index without a central path
  `if/elif` chain.
- `AccessEventSink` / `AccessAnalytics`: accept bounded request events and
  query aggregates independently from audit logging.
- `AuthPolicy`: authenticate Session, Basic, and Bearer credentials and then
  authorize a principal against declared scopes.
- `JobRegistry` / `HealthCheck`: register bounded background work and readiness
  contributors during composition.
- `Clock` / `RequestIdentityResolver`: make time, client identity, and trusted
  proxy behavior explicit and testable.

Plugins and uploaded packages must not dynamically import or execute server
Python code. Extension handlers are registered by trusted application code.

## API compatibility

- Existing public, admin, release, download, and legacy drive-shaped URLs are
  compatibility adapters. Do not silently remove fields or change failure
  semantics.
- New DTOs should use `/api/v1/...` when a compatibility-preserving query mode
  is insufficient. The React client should migrate first; legacy adapters can
  remain for WPF clients and publishing scripts.
- Large legacy payloads may add an explicit compact or paginated representation
  while retaining the default response. Pagination responses expose an exact
  total or a cursor/`hasMore`; clients must not invent totals.
- API changes update backend contract tests and frontend types/client code in
  the same change.
- The long-term contract source is an OpenAPI document with generated frontend
  types. Handwritten TypeScript interfaces remain transitional.

## Access analytics and privacy

Access analytics is not audit logging and is not the legacy plugin download
counter.

- Capture the normalized Flask route template, method, status class, duration,
  response byte count, coarse client family, and UTC timestamp.
- Never persist raw IP addresses, query strings, authorization headers, raw
  user-agent strings, or referrer paths. A unique visitor is a daily rotating
  HMAC derived from the direct peer address. Forwarded addresses require an
  explicit trusted-proxy policy.
- Health/readiness probes, static assets, media, and analytics endpoints are
  excluded from traffic totals. Robot traffic is classified separately.
- Request completion only enqueues into a bounded sink. SQLite writes are
  batched off the request thread; queue drops and writer errors are observable.
- Raw or visitor-level rows have a configured retention period. Daily and route
  aggregates may be retained longer. Admin analytics requires `stats:read`.
- Page views, API requests, downloads, sessions, and unique visitors are
  different metrics and must retain explicit names in API and UI labels.

## Performance guardrails

- A public GET must not hash an artifact, recursively scan storage, rebuild an
  index, or wait for an analytics transaction.
- Release and directory listings are compact and server-paginated before they
  become growth paths. Avoid returning the same artifact in both an owning
  collection and a derived `visible_*` collection.
- Background index checks use cheap change signals and a non-overlapping lease;
  the actual scan produces both the refreshed index and its persisted
  signature.
- Route-level frontend code splitting keeps admin-only Pro Components out of
  the public entry. Decorative media is loaded after critical content and is
  skipped for reduced-motion or data-saving clients.
- Initial guardrails for regression checks are: no unpaginated JSON response
  above 256 KiB, no public initial module preload above 550 KiB gzip, and no
  synchronous request-side filesystem work above 50 ms. Exceptions require a
  measured reason and a focused test or benchmark.

## Verification

Run the smallest focused test first, then the full Web checks:

```powershell
cd Web\Backend
python -m unittest discover -p "test_*.py"

cd ..\Frontend
npm run lint
npm run build
```

For performance-sensitive changes, record response bytes and warm/cold timing
for the affected endpoint. A successful build alone does not verify a payload,
storage, streaming, or browser-loading improvement.
