# ColorVision Marketplace Backend

Flask-based backend serving the ColorVision plugin marketplace, update distribution, and internal management portal.

Authoritative documentation is split by responsibility:

- [Backend composition, configuration, authentication boundary, and health/readiness](../../docs/02-developer-guide/backend/README.md) (`delivery.backend`).
- [File transfer, overwrite, public sharing, and expiry](../../docs/02-developer-guide/backend/file-transfer.md) (`delivery.file-transfer`).
- [Plugin catalog, response projections, index refresh, and version cache](../../docs/02-developer-guide/backend/plugin-catalog.md) (`delivery.plugin-catalog`).
- [Web accounts, roles, passwords, and sessions](../../docs/02-developer-guide/backend/accounts.md) (`delivery.backend-accounts`).
- [HTTP credentials, API keys, and browser CSRF](../../docs/02-developer-guide/backend/authentication.md) (`delivery.backend-auth`).
- [HTTP artifacts, completion counts, cache, HEAD, and compression](../../docs/02-developer-guide/backend/artifact-delivery.md) (`delivery.artifact-delivery`).

This source-adjacent README retains local prerequisites and the public-site, analytics, scheduler, and remaining management contracts not yet moved to those topics. Account, authentication, plugin-read-model, and artifact-response details have one canonical body above. Repository links require the matching full source checkout; they are not a claim that `docs/` ships with a separately copied backend.

## Architecture

### Plugin read models

The [plugin catalog topic](../../docs/02-developer-guide/backend/plugin-catalog.md)
owns `plugin_index`, `package_index`, disk/cache fallback, projection parameters,
hash pending state, full/targeted refresh, and the process-local version map.
GET fallback does not rebuild the index; compact reduces the response after
full detail loading. A refresh reporting ready may still contain per-plugin
errors, and publishing a file does not guarantee that its index refresh succeeded.

### Compact Public Read Models

The React client uses bounded projections while the default responses remain
unchanged for legacy consumers:

| Endpoint | Compact contract |
|----------|------------------|
| `GET /api/site/home?view=compact` | Home-only release counters, previews, update/tool summaries, recent changes, and docs |
| `GET /api/site/changelog?view=compact&page=1&page_size=20` | Latest version plus one bounded rendered changelog page; 5–50 releases per page |
| `GET /api/site/releases?view=compact&page=1&page_size=100&android_page=1&android_page_size=100` | Independently paged Windows and Android archives |
| `GET /api/android/update` | Latest fixed-source Android APK metadata with size, SHA-256, and a bounded download URL |

Windows filters (`major_minor`, `branch`, `kind`, and `era`) apply before exact
counts and pagination. `page_size` and `android_page_size` accept `20..200`.
Each returned group reports its full filtered `visible_count` and the current
slice's `page_item_count`; no group repeats an owning `items` collection.
Plugin list, full/compact/update detail, archive pagination, and compatibility
limits are documented in the canonical plugin topic above.

## Quick Start

Run from `Web/Backend/`. Dependency installation accesses the network. Importing
`app` already composes services and initializes the backend-local
`marketplace.db`; CLI options are parsed afterward. `--storage` changes only
the artifact root, not `config.json` or that database, so a temporary artifact
directory is **not a fully isolated test environment**. Startup may also write
logs, run scheduled jobs, and listen on the configured address. Prepare an
independent backend/config/database for isolated testing, and confirm authority
before starting it. Default production credentials/settings are rejected by the
CLI; debug only relaxes that startup check. See the configuration topic above
for the exact boundary.

```powershell
python -m pip install -r .\requirements.txt
python .\app.py                       # uses config.json
$storage = Join-Path $env:TEMP 'ColorVisionBackend'
python .\app.py --storage $storage    # artifact root only; backend config/database are unchanged
python .\app.py --port 8080           # override port
python .\app.py --debug               # debug mode
```

### Index Management

These commands initialize the Backend and rewrite its database index/cache.
Confirm the actual config, database, and artifact roots first; they are not
read-only probes and `--storage` does not isolate the database.

```powershell
# Refresh the full plugin index
python .\app.py --refresh-index

# Refresh a single plugin's index
python .\app.py --refresh-plugin-index MyPlugin
```

## Admin API

Management endpoints apply the [HTTP authentication contract](../../docs/02-developer-guide/backend/authentication.md).
That topic owns credential precedence, live Session permissions versus issuable
key scopes, key lifecycle, browser 401/403, and CSRF. Authentication alone is
not a full-access grant, and protocols may restrict accepted credential methods.

Legacy API-key Operations relay uses two dedicated scopes and does not accept Basic/session auth:

- `ops:relay` — desktop outbound heartbeat, task polling, receipts, and bounded support events.
- `ops:operator` — list hosts and create catalog-bound tasks. Privileged ServiceHost commands are not valid relay tasks.

Creating a relay key writes the Backend database and grants relay access; do so
only for an authorized deployment with the configuration/database selected.
Create a desktop relay key with `python app.py --create-api-key colorvision-relay --scopes ops:relay`, then set
`COLORVISION_OPERATIONS_RELAY_URL` (HTTPS, or loopback HTTP for development only) and
`COLORVISION_OPERATIONS_RELAY_KEY` in the ColorVision process environment. The desktop initiates every Web connection;
no inbound port or arbitrary command channel is opened. Current desktop builds
use the signed device relay by default: the host sync and task/receipt exchange
are authenticated by the desktop Operations certificate, while task requests
from approved devices retain their P-256 signature for a second verification
on the desktop. Device tasks are limited to empty-payload show/minimize actions
for the current ColorVision main window, an empty-payload recovery of the current
configured message connection and subscriptions, an empty-payload cancellation
request for the active primary flow, an empty-payload restart of the current
ColorVision application, an empty-payload restart of the fixed local Mosquitto
service, and a bounded diagnostic request. The Mosquitto target is injected by
the desktop after it rechecks an idle flow and applicable service state; devices
cannot submit a service name or maintenance parameters. Application restart
uses a signed accepted receipt before shutdown and a final signed receipt from
the replacement process after persistent handoff; window handles, titles,
process or program selectors, flow or node selectors, endpoints, topics,
credentials, paths, arguments, commands, scripts, and arbitrary payload fields
are rejected.
The API-key relay remains available for compatible deployments.

### Endpoint authorization

Use the canonical [endpoint permission and key-scope boundary](../../docs/02-developer-guide/backend/authentication.md#端点permission不等于api-key可申请scope).
In particular, the live role-permission catalog is not the API-key scope catalog;
do not copy a Session permission into a key-creation request.

### Cache Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/cache/status` | Database and cache status |
| POST | `/api/admin/cache/cleanup` | Delete expired cache entries |
| GET | `/api/admin/index/status` | Compact per-index status, counts, timing, and errors |
| GET | `/api/admin/backup/db` | List recognized scheduled and administrator-created snapshots without server paths |
| POST | `/api/admin/backup/db` | Create and privacy-scrub a retained database snapshot |

### Plugin Index

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/admin/index/plugins/refresh` | Refresh all plugin indexes |
| POST | `/api/admin/index/plugins/<id>/refresh` | Refresh single plugin index |

### Jobs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/jobs` | List scheduled jobs with latest run and status totals |
| GET | `/api/admin/jobs/<id>/runs` | Paginated run history, optionally filtered by status |
| POST | `/api/admin/jobs/<id>/run` | Run job immediately; concurrent duplicate runs return `409` |
| POST | `/api/admin/jobs/<id>/enable` | Enable job |
| POST | `/api/admin/jobs/<id>/disable` | Disable job |

Only one `running` row is allowed per job. When the service starts, unfinished
rows left by a previous process are marked `interrupted` before the startup
check runs, so history remains truthful and a crashed run cannot block the job
forever.

### Operational Retention Settings

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/settings/retention` | Read the six allowlisted effective retention values and their limits |
| PUT | `/api/admin/settings/retention` | Atomically replace all six values and apply them to the running service |

The contract intentionally excludes credentials, secrets, storage paths,
listener settings, and Copilot configuration. A write preserves every existing
unexposed JSON key, replaces `config.json` atomically, then updates the live
configuration only after the file is durable. Reducing a value may delete old
artifacts or records when the corresponding publish or scheduled cleanup next
runs; the administrator UI confirms the exact changes before saving.

### Accounts and API keys

The canonical [account lifecycle](../../docs/02-developer-guide/backend/accounts.md)
owns registration defaults and rate limits, profile/query endpoints, role
revisions, password recovery, session revocation, config-admin exceptions,
bulk results, deletion safeguards, and partial-success behavior.
The canonical [authentication topic](../../docs/02-developer-guide/backend/authentication.md)
owns key CRUD/usage, scope selection, expiry, and non-atomic rotation.

When running this backend separately, configure the registration policy
explicitly, protect `upload_auth` and `secret_key`, and retain recovery access.
Never assume ordinary users are initially read-only, that changing the
configured administrator password revokes its old cookies, or that a failed
security-management response means no database state changed.

### Copilot Desktop Sync

`GET /api/copilot/config` accepts the existing Bearer key with
`copilot:config:read` for compatibility. The desktop settings UI uses signed
device proof instead: application version, hardware fingerprint, OS version,
architecture, timestamp, and nonce are authenticated with the installed
ColorVision version key. The version key itself is not sent.

Configure the server with the same release keys used by supported desktop
installations:

```json
{
  "copilot_sync": {
    "version_keys": ["replace-with-the-desktop-version-key"]
  }
}
```

Up to 16 keys may be active during version-key rotation. Missing configuration,
invalid signatures, unsupported device metadata, and proofs older than five
minutes are rejected before model provider credentials are returned.

### Audit Log

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/audit-log?action=&actor=&target=&since=&until=&limit=&offset=` | View audit log with an exact filtered `total` |

Audit rows are retained for 365 days by default. The daily
`admin_data_retention` job applies the cutoff to the live database and every
recognized database snapshot, so a backup cannot bypass
the audit retention contract.
Pagination accepts `limit` from 1 through 500 and a non-negative `offset`;
invalid values return HTTP 400 instead of becoming an unbounded SQLite query.
The administrator viewer localizes known event, actor, target, and detail
contracts while preserving raw action codes. Source IP and user-agent data are
available only in the authenticated event drawer for incident investigation.

### Deployment History

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/deployments?status=&source=&commit=&limit=&offset=` | View sanitized, latest-first NAS deployment history |

The response includes exact filtered totals plus aggregate status/source and
malformed-record counts. It intentionally omits server names, absolute paths,
runtime log paths, and raw errors; failed records expose only a coarse failure
category. The deployment writer keeps 500 valid records by default.

### Feedback Inbox

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/feedback?status=&query=&limit=&offset=` | List feedback with aggregate status and attachment totals |
| GET | `/api/admin/feedback/<id>` | Read one feedback submission and its attachment inventory |
| GET | `/api/admin/feedback/<id>/attachments/<name>` | Download a direct diagnostic attachment |
| PUT | `/api/admin/feedback/<id>/status` | Move feedback between `new`, `in_progress`, and `resolved` |

Feedback remains filesystem-authoritative under the existing `Feedback`
storage directory. The inbox preserves legacy directories with missing or
malformed metadata, displays their attachment inventory, and never rewrites
the submitted `feedback.json`. Administrator workflow state is persisted in a
separate atomic `.admin.json` sidecar. Attachment delivery rejects traversal,
metadata/state files, symbolic links, and non-direct files. Downloads and
status changes are recorded in the administrator audit log.

### Operations Overview

`GET /api/admin/operations/overview?hostLimit=100&activityLimit=100` powers the
read-only `/admin/operations/hosts` page. It reports exact host/task/session
summary counts, signed host-identity and paired-device status, a bounded host
list, task origin and lifecycle state, receipt counts, and support-session
state. A heartbeat is treated as online for 90
seconds, matching the desktop Relay's 20-second polling cadence without hiding
short network interruptions.

The endpoint deliberately returns neither host certificates, device public
keys, request signatures/nonces/bodies, task payloads, receipt evidence,
support message bodies, nor arbitrary snapshot keys. Desktop heartbeats are
projected back onto the fixed `OperationsSafeSnapshot` fields before the React
client receives them. Legacy operator task creation remains on the separate
`ops:operator` API-key contract, while paired devices use the signed Relay
endpoint; the administrator page itself cannot dispatch work.

### Stats

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/stats/overview` | Download, index, and today's traffic summary |
| GET | `/api/admin/stats/traffic?days=30&limit=10` | HTTP traffic plus separately labeled SPA page views, Core Web Vitals, top pages, and recorder health |
| GET | `/api/admin/perf/summary` | Process-local slow requests plus recent slow or failed scheduler runs |

React sends `POST /api/v1/analytics/events` with either an exact `page_view`
or `web_vital` payload. Cross-origin browser writes are rejected; the endpoint
caps the body at 4 KiB, maps known paths to fixed templates such as
`/plugins/:pluginId` and `/browse/*`, and rejects extra fields. Browser bots are
ignored. Accepted events enter the same bounded asynchronous writer used by
HTTP access analytics, so telemetry never performs a SQLite transaction on the
request thread.

`days` accepts `1..365`; `limit` accepts `1..100`. Rates and client shares are
percentages in the range `0..100`. Response volume is based only on the existing
HTTP `Content-Length` header. A missing or invalid header is counted as zero, so
analytics never buffers or consumes streamed/file responses. `HEAD`, 1xx, 204,
205, and 304 responses are also counted as zero because they do not transfer a
response body. Schema migration v6 removes the previously declared `HEAD` bytes
from both route and daily historical aggregates. Schema migration v7 adds exact
4xx and 5xx counters for new requests. The compatible `errorResponses` total is
retained; older errors that cannot be separated reliably are returned through
`unclassifiedErrorResponses` instead of being guessed into either category.
Schema migration v8 records when the reporting calendar changes from the old
UTC boundary. New traffic and `downloadsToday` use the configured reporting
offset; existing daily aggregates remain unchanged and are exposed through the
summary's legacy-calendar metadata instead of being guessed into adjacent days.

The performance summary is intentionally a lightweight diagnostic companion to
the aggregate traffic report, not a second analytics store. Slow requests are
sanitized to method, route path, status, duration, and UTC occurrence time, then
kept in a bounded 100-entry process-local buffer. The response exposes the
active threshold, process start time, and buffer usage; the buffer resets
whenever the Web process restarts. Slow scheduler runs remain sourced from the
existing job history.

`summary.uniqueVisitorDays` is the sum of each day's unique visitors (visitor-days),
not a cross-day unique-person count, because the privacy identifier rotates every
configured reporting day. `today.uniqueVisitors` and each `daily[].uniqueVisitors` remain true
within-day unique counts. Client aggregates therefore expose
`clients[].uniqueVisitorDays`; the API deliberately does not publish a misleading
multi-day `uniqueVisitors` field.

#### Access Analytics Privacy and Retention

Access analytics stores daily aggregate counters rather than request logs. Route
statistics use the Flask route template (for example `/api/plugins/<plugin_id>`),
not the raw URL. Query strings and referrer paths are never accepted by the event
boundary. User-agent strings are reduced in memory to `desktop`, `mobile`,
`tablet`, `bot`, or `other`, and are not stored verbatim. A visitor is represented
by a daily HMAC derived from the configured application secret and remote address;
the raw address is never persisted, and identifiers cannot be linked across days.
SPA page views reuse that daily unlinkable identifier but store it only in a
route/day uniqueness table. Web Vitals store the metric name, bounded numeric
value, quality bucket, fixed route template, and day; metric IDs, DOM targets,
full URLs, queries, referrers, raw addresses, and full user agents are not
accepted or persisted. The same retention job scrubs HTTP, page-view, and Web
Vital tables in both the live database and recognized snapshots.

Health/readiness probes, static assets, media, favicon/brand assets, and the stats
or performance-observability endpoints themselves are excluded. Production
requests enqueue sanitized events into a bounded in-memory queue and a background
worker writes grouped SQLite
transactions. Queue saturation or write failures drop only the analytics event;
they do not delay or fail the HTTP response. Recorder state is returned as
`pending`, `dropped`, `lastError`, `lastFlushAt`, and `capacity`.

Configuration defaults:

| Key | Default | Meaning |
|-----|---------|---------|
| `app_release_keep_count` | `5` | Newest main application release packages retained after publishing |
| `plugin_package_keep_count` | `3` | Newest package versions retained per plugin after publishing |
| `access_analytics_enabled` | `true` | Enable request aggregation |
| `access_analytics_queue_size` | `4096` | Maximum queued events before non-blocking drops |
| `access_analytics_batch_size` | `128` | Maximum events grouped per writer pass |
| `access_analytics_flush_interval_seconds` | `0.5` | Writer wait/flush interval |
| `access_analytics_retention_days` | `90` | Reporting-calendar days retained by the scheduled cleanup |
| `reporting_utc_offset_minutes` | `480` | Fixed UTC offset used by daily dashboard metrics (`UTC+08:00`) |
| `job_run_retention_days` | `30` | Completed scheduler runs retained; each job's latest run and running rows are always kept |
| `audit_log_retention_days` | `365` | Administrator audit rows retained in the live database and recognized snapshots |
| `admin_db_backup_keep_count` | `10` | Newest recognized scheduled or administrator-created database snapshots retained; minimum 2 |

The daily `database_backup` task creates a transactionally consistent SQLite
snapshot and applies the same privacy cleanup and rotation contract used by the
administrator backup action. The same scheduled retention pass also removes expired access rows and
all browser sessions, login/registration limit sources, and password-recovery
workflow rows from recognized `marketplace_backup_YYYYMMDD_HHMMSS.db`
snapshots. User password hashes, profiles, roles, and permissions remain in the
restorable snapshot, while its authentication version is advanced once so even
a legacy signed browser cookie cannot survive a restore. A newly created database backup is scrubbed and
integrity-checked before it is reported as successful, so a restored snapshot
cannot reactivate a copied browser session or bypass visitor retention.

`GET /api/admin/backup/db` lists only recognized snapshot names, UTC creation
times, and sizes. Neither it nor `POST /api/admin/backup/db` returns filesystem
paths. The create endpoint also applies the audit cutoff and immediately
rotates exact `marketplace_backup_YYYYMMDD_HHMMSS.db` files. The backup created
by the current request is explicitly protected. Non-matching names, symbolic
links, paths outside the database directory, and snapshots that fail audit
cleanup are never removed automatically. The response includes a
`backup_retention` result with the retained limit, removal count and byte count,
and any rotation errors.

## Admin Pages

| Path | Description |
|------|-------------|
| `/admin/` | Overview dashboard |
| `/admin/cache` | Cache and index management |
| `/admin/api-keys` | API Key lifecycle management |
| `/admin/users` | Registered account status management |
| `/admin/jobs` | Scheduled jobs, single-flight actions, status totals, and filterable run history |
| `/admin/deployments` | Sanitized NAS deployment history and retention results |
| `/admin/feedback` | Feedback inbox, diagnostic attachments, and processing lifecycle |
| `/admin/audit` | Audit log viewer |
| `/admin/traffic` | Privacy-preserving request traffic and recorder health |
| `/admin/settings` | Browser appearance plus server-side operational retention policies |

## API Key Authentication

See the [canonical API-key contract](../../docs/02-developer-guide/backend/authentication.md)
for request fields, one-time plaintext responses, scope/expiry validation,
last-used persistence, and rotation failure boundaries. Key creation and package
publication are state-changing operations, not setup probes.

## Scheduled Jobs

| Job | Interval | Description |
|-----|----------|-------------|
| `plugin_index_check` | 5 min | Signature/state check; current refresh is full-plugin, not changed-plugin-only; see the canonical plugin topic |
| `release_index_check` | 10 min | Compare release artifacts signature; refresh only if changed |
| `update_index_check` | 10 min | Compare Update directory signature; refresh only if changed |
| `tool_index_check` | 10 min | Compare Tool directory signature; refresh only if changed |
| `cache_cleanup` | 1 hour | Delete expired cache entries |
| `password_recovery_cleanup` | 1 hour | Expire idle sessions and bound login, registration, and password-recovery security state |
| `transfer_file_cleanup` | 1 hour | Delete anonymous transfer files and share links after their 24-hour lifetime |
| `access_analytics_retention` | 1 day | Delete access aggregates older than the configured retention window |
| `job_history_retention` | 1 day | Delete completed job runs older than the configured retention window while preserving current state |
| `admin_data_retention` | 1 day | Delete expired audit rows, scrub transient account-security state from snapshots, then bound recognized DB backups |
| `startup_index_check` | Once | Ensure all indexes are populated on startup |

The scheduler starts automatically when `scheduler_enabled` is true (default). In debug mode, it only starts in the Flask reloader child process to avoid duplicate threads. Set `scheduler_enabled: false` in config.json to disable.

Intervals above are initial defaults; persisted job settings determine the live schedule.
Plugin startup, signature/state skip conditions, pre-scan signature persistence,
and refresh errors are defined in the canonical plugin topic. A job success
label alone is not proof that every index entry is current.

## Deployment Notes

The following actions can initialize or modify the selected Backend database,
configuration, and artifact state; backups/retention may also remove retained
files. Confirm the target and authority before running them. See Quick Start
for the non-isolating `--storage` boundary.

1. **First deploy**: Run `python app.py --refresh-all-indexes` to populate all indexes (plugins, releases, updates, tools).
2. **Manual file changes**: If you manually modify storage directories, either:
   - Wait for the periodic scheduler check, or
   - Call `POST /api/admin/index/refresh-all`
3. **Database backup**: `POST /api/admin/backup/db` creates, privacy-scrubs, integrity-checks, and rotates a timestamped backup of `marketplace.db`.
4. **API Key security**: Keys are shown only once at creation. Revoke and rotate if compromised. Scopes are validated against a whitelist at creation time; expiry timestamps are normalized to UTC, and expired or malformed legacy records fail closed.
5. **Config**: Use `/admin/settings` for the account-access policy and six safe live retention policies. Edit `config.json` directly for protected or restart-bound values such as `storage_path`, `upload_auth`, `secret_key`, and scheduler settings.

### Large File Transfer

The canonical [file-transfer contract](../../docs/02-developer-guide/backend/file-transfer.md)
covers whole-file and resumable endpoints, `file:transfer` authorization,
anonymous-session ownership, chunk/offset limits, public sharing, overwrite,
expiry, and cleanup. Keep those rules there rather than maintaining a second
endpoint/configuration table here.

Operational prerequisites still apply when using this backend alone: confirm
the configured transfer directory and reverse-proxy body/timeout limits before
uploading. Authorized uploads can overwrite same-name files; existing share
tokens can then expose the replacement content. Whole-file PUT/POST may retain
an existing temporary share's expiry, so a successful signed-in upload is not
an unconditional promise of permanent retention. Upload, share, and delete
actions require separate authorization; a documentation example is not consent
to perform them.

### Response Security and HEAD Semantics

The canonical [artifact-response topic](../../docs/02-developer-guide/backend/artifact-delivery.md)
owns the response security baseline, Range/ETag/cache behavior, server-iteration
completion counts, HEAD preparation side effects, and buffered JSON gzip.
The [authentication topic](../../docs/02-developer-guide/backend/authentication.md)
owns cookies, CSRF branch conditions, 401/403, and POST logout.

HEAD does not increment plugin completion statistics, but can still repair old
update storage, reconcile transfer metadata, or trigger expiry cleanup.
Do not use it as a blanket no-write probe. Existing response cache headers are
not overwritten by the generic sensitive-API `no-store` fallback.

## Disk Scan Points

The following are navigation hints, not an exhaustive no-I/O guarantee.
Plugin index hits, missing-row fallback, request-local caching, and version-map
staleness are defined in the canonical plugin topic.

### Index-backed and cached reads

- `GET /api/plugins` / `GET /api/plugins/<id>` — see the canonical plugin read-model contract
- `GET /api/site/releases` — reads from `release_index` via `scan_app_release_artifacts`
- `GET /api/site/updates` — reads from `update_index`
- `GET /api/site/tools` — reads from `tool_index`
- `GET /api/site/home` — reads from `release_index`, `update_index`, `tool_index` for previews
- `GET /api/tool/cvwindowsservice/releases` — cached with signature-based invalidation

### Live file access and related probes

- `GET /api/site/browse/<path>?q=<name>&type=all|directory|file&limit=200&offset=0` — reads and filters one live directory before pagination (no recursion). Anonymous callers only see published application, History, Plugins, Spectrum, Update, and Tool artifacts; authenticated administrators retain full storage access.
- `GET /plugins/<id>/icon` — reads icon file for ETag/Last-Modified headers
- `GET /download/<path>` — serves public artifacts directly from disk. Operational storage requires administrator authentication, while Transfer keeps its separate file-transfer authorization policy.
- `GET /api/app/changelog` — reads `CHANGELOG.md` (single file read)
- `GET /api/app/latest-version` — reads in-memory `LATEST_RELEASE` cache (warmed at startup, refreshed on upload)
- `GET /api/android/update` — reads the latest indexed root APK and caches its SHA-256 by path, size, and modification time
- `GET /api/health` — reports process metadata; it does not probe storage or database readiness.
- `GET /api/ready` — may create storage/Plugins directories, checks writability and a database `SELECT 1`, and requires nonempty upload credentials. Index status is informational, not a readiness gate; see the canonical startup/readiness contract.

### Scheduler signature checks

- `release_index_check` — two-level History walk (major/branch/file), no deep rglob
- `update_index_check` — single `Update/` directory listing
- `tool_index_check` — single `Tool/` directory listing
- `plugin_index_check` — `plugin_catalog_signature()` over Plugins directory

### Upload/publish refresh dispatch

These are refresh entry points, not atomic file/index completion guarantees.

- `POST /api/packages/publish` → best-effort `refresh_plugin_index` for that plugin
- `PUT /upload/<path>` → refreshes `release_index`, `update_index`, or `tool_index` based on path
- `POST /api/tool/cvwindowsservice/publish` → refreshes `tool_index`

## New Modules

| Module | Purpose |
|--------|---------|
| `routes/auth_adapters.py` / `services/auth_policy.py` / `services/permission_service.py` | Flask authentication adapters, common credential/scope policy, and live role permissions |
| `services/auth_middleware.py` | Deprecated import compatibility shim; not a separate authentication implementation |
| `services/storage_events.py` | Post-upload/publish index refresh dispatcher |
| `cli.py` | CLI argument parsing and command execution |
| `db/schema_version.py` | Schema version tracking and migrations |
| `routes/admin_api.py` | Admin REST API (cache, index, jobs, audit, deployments, keys, perf) |
| `services/deployment_history.py` | Sanitized deployment-history query and pagination |
| `services/performance_observability.py` | Bounded slow-request samples and performance-summary shaping |
| `services/password_recovery_service.py` | Coalesced administrator-assisted password recovery lifecycle |
| `ports/operations_support.py` / `db/repositories/operations_support.py` | Atomic Operations support-session persistence boundary |
| `routes/frontend_spa.py` | React SPA static hosting and `/admin` auth gate |
| `routes/pages.py` | Public site-data and download APIs |
| `routes/public_pages.py` | Session login/logout APIs and form redirects |

### Config Options

```json
{
  "storage_path": "H:\\ColorVision",
  "host": "0.0.0.0",
  "port": 9998,
  "secret_key": "change-this-in-production",
  "upload_auth": {"username": "admin", "password": "admin"},
  "scheduler_enabled": true,
  "plugin_index_check_interval_seconds": 300
}
```

## Testing

Inspect each test's config/database isolation before execution. Importing
`app` itself initializes Backend state; a temporary artifact root alone is
insufficient isolation. Documentation-only changes do not require starting it.

```powershell
python -m unittest discover -p "test_*.py"
```

## Existing API Compatibility

Compatibility entry points (current detailed behavior is defined by the linked topics, not a blanket compatibility guarantee):

- `GET /api/plugins` — Search plugins (now reads from SQLite index, falls back to disk scan if index is empty)
- `GET /api/plugins/<id>` — Plugin detail (indexed missing hashes report `hashPending`; fallback may read/hash files)
- `GET /api/plugins/<id>/latest-version` — Latest version
- `GET /api/packages/<id>/<version>` — Download package
- `POST /api/packages/publish` — Publish (now also supports Bearer auth with `plugin:publish` scope)
- `PUT /upload/<path>` — Legacy upload
- `GET /api/health` — Health check
- `GET /api/ready` — Readiness check
- `GET /api/stats` — Download statistics
- `POST /api/feedback` — Submit feedback

Default plugin detail retains its full projection; compact/update have distinct
field and pagination contracts. See the canonical plugin topic before relying
on raw Markdown, per-version fields, hash availability, or history bounds.
