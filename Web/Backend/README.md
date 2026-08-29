# ColorVision Marketplace Backend

Flask-based backend serving the ColorVision plugin marketplace, update distribution, and internal management portal.

## Architecture

### Index Model

The backend uses a **SQLite index** to serve plugin catalog requests without scanning the file system on every call.

**Tables:**
- `plugin_index` — Persistent read-model for the plugin catalog
- `package_index` — Persistent read-model for plugin package versions
- `index_state` — Tracks refresh status per scope (plugins/releases/tools)
- `cache_entry` — Key-value cache with TTL and signature-based invalidation
- `download_log` — Download statistics
- `users` — Admin/operator/viewer accounts
- `api_keys` — API Key lifecycle management (only hash stored)
- `audit_log` — All admin operations
- `scheduled_jobs` / `job_runs` — Job scheduling and execution history
- `web_page_daily` / `web_vital_daily` — Aggregate-only SPA page views and Core Web Vitals

### Three Sync Triggers

1. **Startup** — If `plugin_index` is empty, a background refresh populates it. If populated, only a lightweight signature check runs.
2. **Publish** — After `/api/packages/publish` or `/upload`, the specific plugin's index entry is refreshed immediately.
3. **Periodic** — A background job (`plugin_index_check`) runs every 5 minutes, compares the Plugins directory signature against the stored signature, and triggers a targeted refresh if changes are detected.

### Request Flow

```
GET /api/plugins
  → Check plugin_index table (fast, no disk scan)
  → If empty: fallback to disk scan + write to index
  → Return results
```

### Compact Public Read Models

The React client uses bounded projections while the default responses remain
unchanged for legacy consumers:

| Endpoint | Compact contract |
|----------|------------------|
| `GET /api/site/home?view=compact` | Home-only release counters, previews, update/tool summaries, recent changes, and docs |
| `GET /api/site/changelog?view=compact&page=1&page_size=20` | Latest version plus one bounded rendered changelog page; 5–50 releases per page |
| `GET /api/site/releases?view=compact&page=1&page_size=100&android_page=1&android_page_size=100` | Independently paged Windows and Android archives |
| `GET /api/android/update` | Latest fixed-source Android APK metadata with size, SHA-256, and a bounded download URL |
| `GET /api/plugins?Page=1&PageSize=20` | Paged plugin summaries plus the complete category filter list, so the web page needs one catalog request |
| `GET /api/plugins/<id>?view=compact&archive_page=1&archive_page_size=20` | Web detail metadata and rendered docs plus one bounded, order-preserving History page; raw Markdown is omitted |
| `GET /api/plugins/<id>?view=update` | Desktop update metadata without README or per-version changelog duplication |

Windows filters (`major_minor`, `branch`, `kind`, and `era`) apply before exact
counts and pagination. `page_size` and `android_page_size` accept `20..200`.
Each returned group reports its full filtered `visible_count` and the current
slice's `page_item_count`; no group repeats an owning `items` collection.
Plugin detail archive pages accept 5–100 items. The default full detail and
desktop `view=update` contracts remain unchanged for existing clients.

## Quick Start

```powershell
python -m pip install -r .\requirements.txt
python .\app.py                       # uses config.json
$storage = Join-Path $env:TEMP 'ColorVisionBackend'
python .\app.py --storage $storage    # override storage path
python .\app.py --port 8080           # override port
python .\app.py --debug               # debug mode
```

### Index Management

```powershell
# Refresh the full plugin index
python .\app.py --refresh-index

# Refresh a single plugin's index
python .\app.py --refresh-plugin-index MyPlugin
```

## Admin API

All admin endpoints require authentication (session login or Basic Auth or Bearer API Key).

Browser-originated API requests carry `X-ColorVision-Web: 1` and intentionally
receive a plain `401` without a `WWW-Authenticate` challenge so the React client
can recover expired sessions without opening the browser's Basic Auth dialog.
Headerless native clients keep the Basic challenge, while protected browser
navigations redirect to `/login` with an internal `next` path.

Session and Basic Auth always have full access. Bearer API Key access is controlled by per-endpoint scopes:

Legacy API-key Operations relay uses two dedicated scopes and does not accept Basic/session auth:

- `ops:relay` — desktop outbound heartbeat, task polling, receipts, and bounded support events.
- `ops:operator` — list hosts and create catalog-bound tasks. Privileged ServiceHost commands are not valid relay tasks.

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

Successful Bearer authentication still checks the active flag, expiry, secret,
and scopes on every request. Only advisory `last_used_at` persistence is
coalesced to at most once per key per minute to avoid turning polling reads into
continuous SQLite writes.

### Endpoint Scopes

| Endpoint | Required Scope |
|----------|---------------|
| GET `/api/admin/cache/status` | `cache:read` |
| POST `/api/admin/cache/cleanup` | `cache:refresh` |
| POST `/api/admin/index/plugins/refresh` | `cache:refresh` |
| POST `/api/admin/index/plugins/<id>/refresh` | `cache:refresh` |
| POST `/api/admin/index/releases/refresh` | `cache:refresh` |
| POST `/api/admin/index/updates/refresh` | `cache:refresh` |
| POST `/api/admin/index/tools/refresh` | `cache:refresh` |
| POST `/api/admin/index/refresh-all` | `cache:refresh` |
| GET `/api/admin/index/status` | `cache:read` |
| GET `/api/admin/backup/db` | `admin:*` |
| POST `/api/admin/backup/db` | `admin:*` |
| GET `/api/admin/jobs` | `jobs:read` |
| GET `/api/admin/jobs/<id>/runs` | `jobs:read` |
| POST `/api/admin/jobs/<id>/run` | `jobs:write` |
| GET `/api/admin/operations/overview` | `admin:*` |
| POST `/api/admin/jobs/<id>/enable` | `jobs:write` |
| POST `/api/admin/jobs/<id>/disable` | `jobs:write` |
| GET `/api/admin/stats/overview` | `stats:read` |
| GET `/api/admin/stats/traffic` | `stats:read` |
| GET `/api/admin/audit-log` | `admin:*` |
| GET `/api/admin/deployments` | `admin:*` |
| GET `/api/admin/settings/retention` | `admin:*` |
| PUT `/api/admin/settings/retention` | `admin:*` |
| GET `/api/admin/settings/accounts` | `admin:*` |
| PUT `/api/admin/settings/accounts` | `admin:*` |
| User account management | `admin:*` |
| API Key management | `admin:*` |

`admin:*` grants access to all endpoints. Session and Basic Auth (validated against `upload_auth` config) always have full access.

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

### Account Access Settings

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/settings/accounts` | Read whether public self-registration is enabled |
| PUT | `/api/admin/settings/accounts` | Atomically update the registration policy and apply it immediately |

Public registration fails closed and is disabled by default. When disabled,
the login UI hides the registration entry and `POST /api/auth/register`
returns `403`; existing accounts and administrator-created accounts are not
affected. Enabling the policy permits any visitor who can reach the site to
create a regular `user` account, never an administrator account.
Public signup is velocity-limited per source address: at most 20 attempts per
10 minutes and 5 successful accounts per hour. The counters and concurrent
reservations are stored in SQLite, survive restarts, and return `429` with a
`Retry-After` header when a limit is active. Administrator-created accounts do
not consume the public-signup quota.

Password recovery uses an administrator-assisted workflow because the service
does not have an outbound mail dependency. `POST /api/auth/password-recovery`
always returns the same `202` response for existing, missing, inactive, and
configuration-managed accounts. Matching database accounts receive one
coalesced pending request, visible in user management; resetting the account
password resolves it and still requires the user to replace the temporary
password at next login.

### API Keys

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/api-keys` | List API keys |
| GET | `/api/admin/api-keys/scopes` | List the authoritative scope catalog and default scopes |
| POST | `/api/admin/api-keys` | Create new key (returns plaintext once) |
| POST | `/api/admin/api-keys/<id>/revoke` | Revoke key |
| POST | `/api/admin/api-keys/<id>/rotate` | Rotate key (revoke old, create new) |
| GET | `/api/admin/api-keys/<id>/usage` | Get public key metadata and recent audited writes |

`expires_at` must be a future ISO 8601 timestamp. The service normalizes it to
UTC; omitting it from the HTTP create request applies the default 90-day
expiry. List and usage responses include the effective `status` (`active`,
`expired`, `revoked`, or `invalid_expiry`) plus `last_used_at`. Descriptions are
stored independently from names and survive key rotation. The scope catalog is
the single source of truth for the admin UI and includes human-readable labels,
categories, and least-privilege guidance.

The usage response includes recent audited management writes attributed to the
key prefix, but deliberately excludes request IP addresses and user agents. It
is not a request counter: authenticated reads only update `last_used_at`, at
most once per minute. Legacy records with an invalid expiry fail closed and
cannot authenticate.

### User Accounts

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/users` | List accounts; supports `q`, `role`, `origin`, `status`, `password_state`, `recovery_state`, allowlisted `sort_by`/`sort_order`, `limit`, and `offset` |
| GET | `/api/admin/users/<id>/details` | Inspect one account's profile, pending recovery request, effective permissions, active sessions, and paged recent activity |
| POST | `/api/admin/users` | Create a `user` or `admin` account |
| DELETE | `/api/admin/users/<id>` | Permanently delete a disabled database account after confirming its current username |
| PUT | `/api/admin/users/<id>/profile` | Update an account's display name and email |
| PUT | `/api/admin/users/<id>/role` | Change an account role and revoke its existing sessions |
| POST | `/api/admin/users/<id>/password` | Reset a password, resolve pending recovery, revoke existing sessions, and require the account to replace the temporary password at next login |
| POST | `/api/admin/users/<id>/password-change-required` | Keep the current password but revoke sessions and require the account to replace it at next login |
| POST | `/api/admin/users/<id>/sessions/revoke` | Force the account's valid browser sessions offline without disabling it |
| POST | `/api/admin/users/bulk-security` | Force logout or require a password change for up to 100 selected accounts with per-account outcomes |
| POST | `/api/admin/users/<id>/enable` | Re-enable an account and revoke its previous sessions |
| POST | `/api/admin/users/<id>/disable` | Disable an account, revoke sessions, resolve pending recovery, and clear login-failure state |
| GET | `/api/admin/login-security` | List active login-failure windows and source addresses |
| POST | `/api/admin/login-security/unlock` | Clear one username's login-failure window |
| GET | `/api/admin/registration-security` | List live public-registration counters by source address |
| POST | `/api/admin/registration-security/clear` | Clear one source's completed registration counters while preserving in-flight reservations |
| GET | `/api/admin/permissions` | List the role/permission matrix with active and total database-account counts per role |
| PUT | `/api/admin/roles/<role>/permissions` | Replace an editable role's permissions, optionally rejecting stale `expected_revision` values |
| GET | `/api/account` | Read the signed-in user's own profile plus effective permission codes and display metadata |
| PUT | `/api/account` | Update the signed-in user's display name and email |
| POST | `/api/auth/password-recovery` | Submit a privacy-preserving administrator-assisted password recovery request |
| PUT | `/api/account/password` | Change the signed-in database user's password |
| GET | `/api/account/sessions` | List the signed-in user's active browser sessions |
| DELETE | `/api/account/sessions/<id>` | Revoke one other browser session |
| DELETE | `/api/account/sessions/others` | Revoke every session except the current browser |
| GET | `/api/account/activity` | Read the current account's privacy-scoped activity timeline |

The current session account cannot be disabled or assigned a different role,
forced offline or deleted from user management, and the last active administrator cannot
be disabled or demoted. User list responses include the number of non-revoked,
authentication-version-matched browser sessions for each account. When an
administrator resets their own password, the current browser session is updated
to the new authentication version while all other sessions are revoked. Accounts
created by an administrator and accounts whose password is reset by another
administrator receive a temporary-password marker. Their browser session is
restricted to their own profile, session/activity reads, logout, and password
change until they replace that password; effective permissions and admin access
are restored immediately afterward. Self-registration and an administrator
changing their own password do not set this marker. Administrators can also set
the marker without learning or replacing the current password; this expires all
active sessions, after which the user signs in with the existing password and
must immediately choose a new one.

The configured `upload_auth` administrator remains authoritative and is never
copied into the user table during startup. Its username is reserved from public
or administrator-created registration. If an older installation already has a
same-name database shadow record, the record is labelled as the configuration
administrator and cannot be edited, disabled, demoted, password-reset, or
forced into the database-account password-change flow from user management;
only the current configuration credential can start a new
browser session for that username.

Permanent deletion is intentionally a two-stage operation. An administrator
must disable the database account first, then confirm the account's current
username in the delete request. Deletion removes the profile, password hash,
session history, password-recovery records, and matching login-failure sources;
administrator audit records remain available as historical evidence. The
configured administrator and the current browser account cannot be deleted.

The account-security page combines login protection with public-registration
protection. Registration source rows distinguish blocked and tracking states,
show both attempt and successful-account windows, refresh when a window expires,
and can be cleared manually. A clear is concurrency-safe: existing counters are
removed, but any request already holding a registration reservation remains
accounted for when it completes. Manual clears are written to the audit log.

New database-account passwords are validated consistently across public
registration, administrator creation/reset, and self-service password changes.
They must contain 15–128 Unicode characters; spaces and passphrases are allowed,
and no character-class composition rule is imposed. This policy applies only
when a password is newly set: existing password hashes and the configured
`upload_auth` administrator credential remain valid and are not migrated.

Administrator-assisted password-recovery requests remain pending for seven
days after the most recent recorded submission. Expired requests are retained
as system-resolved history for audit purposes, disappear from pending-account
filters and totals, and no longer prevent the account from submitting a fresh
request. The account-details drawer shows the effective expiration time.

Account security transitions settle related state together. Disabling an
account revokes its sessions, resolves any pending recovery request as
`account_disabled`, and clears persisted login-failure sources. Re-enabling also
clears failures accumulated while the account was inactive without reviving old
sessions. Administrator password resets and both single-account and bulk
"require password change" actions clear the same login-failure state, so an
administrator-assisted recovery cannot leave the user blocked by an earlier
throttle window. API responses and audit details report the affected counts.

Database accounts persist `password_changed_at`. New registrations initialize
it with account creation, administrator resets and self-service changes advance
it, while a "require password change" marker alone does not pretend the secret
was replaced. Existing installations backfill the value from account creation
during schema v26. Personal and administrator account details show the value in
the same localized time format as login and session activity.

`GET /api/auth/session` includes `public_registration_enabled` so the public
navigation and login page reflect the server-enforced policy. The value is a
capability hint only; the registration endpoint rechecks the live setting for
every request.

Public registration is enabled in the shipped default configuration. New
accounts retain the `user` role but are initially granted the same function
permissions as the existing administrator. The grants are stored in SQLite in
`roles`, `permissions`, and `role_permissions`; they can be reduced later from
the permissions page without changing the configured administrator contract.
Session payloads expose `can_access_admin` and the effective permission codes,
while every protected backend endpoint remains the authoritative enforcement
point. The administrator role is fixed at full access.

Role-permission updates return an authoritative change summary containing the
added and removed codes, the number of active accounts immediately affected,
and the new revision. The same fields are persisted in the audit log, while
registered-user requests read the live role matrix so an existing session
cannot continue using a removed permission.

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

### Creating a Key

```powershell
curl.exe -X POST http://localhost:9998/api/admin/api-keys `
  -u admin:password `
  -H "Content-Type: application/json" `
  -d '{"name": "CI Pipeline", "scopes": "plugin:publish,release:publish"}'
```

The response includes the full key (shown only once):
```json
{
  "id": 1,
  "name": "CI Pipeline",
  "key": "cvmp_a1b2c3d4_e5f6g7h8i9j0...",
  "key_prefix": "a1b2c3d4",
  "scopes": "plugin:publish,release:publish"
}
```

### Using a Key

```powershell
curl.exe -X POST http://localhost:9998/api/packages/publish `
  -H "Authorization: Bearer cvmp_a1b2c3d4_e5f6g7h8i9j0..." `
  -F "PluginId=MyPlugin" `
  -F "Version=1.0.0" `
  -F "package=@.\MyPlugin-1.0.0.cvxp"
```

### Available Scopes

- `plugin:read` — Read plugin catalog
- `plugin:publish` — Publish plugin packages
- `release:publish` — Publish application releases
- `file:transfer` — Upload, download, list, and delete transfer files
- `cache:read` — Read cache status
- `cache:refresh` — Refresh caches
- `stats:read` — Read statistics
- `jobs:read` — Read job status
- `jobs:write` — Run/enable/disable jobs
- `admin:*` — Full admin access

## Scheduled Jobs

| Job | Interval | Description |
|-----|----------|-------------|
| `plugin_index_check` | 5 min | Compare Plugins directory signature with stored signature; refresh only if changed |
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

Signature-based check: each index check computes a directory signature and compares it with the stored signature in `index_state`. If they match, no refresh is triggered. The signature is updated after each successful refresh.

## Deployment Notes

1. **First deploy**: Run `python app.py --refresh-all-indexes` to populate all indexes (plugins, releases, updates, tools).
2. **Manual file changes**: If you manually modify storage directories, either:
   - Wait for the periodic scheduler check, or
   - Call `POST /api/admin/index/refresh-all`
3. **Database backup**: `POST /api/admin/backup/db` creates, privacy-scrubs, integrity-checks, and rotates a timestamped backup of `marketplace.db`.
4. **API Key security**: Keys are shown only once at creation. Revoke and rotate if compromised. Scopes are validated against a whitelist at creation time; expiry timestamps are normalized to UTC, and expired or malformed legacy records fail closed.
5. **Config**: Use `/admin/settings` for the account-access policy and six safe live retention policies. Edit `config.json` directly for protected or restart-bound values such as `storage_path`, `upload_auth`, `secret_key`, and scheduler settings.

### Large File Transfer

The protected transfer area is configured by `transfer_upload_dir` (default: `Transfer`, relative to `storage_path`). It is intentionally limited to files directly inside that folder; subdirectories and path traversal are rejected.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/transfer/files` | List transfer files |
| PUT/POST | `/api/transfer/files/<filename>` | Stream-upload a file without the package upload size limit |
| POST | `/api/transfer/uploads` | Create or recover a resumable upload session from file metadata and fingerprint |
| GET | `/api/transfer/uploads/<upload_id>` | Read the server-confirmed resumable offset |
| PATCH | `/api/transfer/uploads/<upload_id>` | Append one chunk at the required `Upload-Offset` |
| GET | `/api/transfer/shares/<token>` | Read public metadata for one unguessable file share |
| GET | `/api/transfer/shares/<token>/download` | Download the exact file addressed by a share token |
| GET | `/api/transfer/files/<filename>` | Download a transfer file |
| DELETE | `/api/transfer/files/<filename>` | Delete a transfer file |

The React UI exposes resumable uploads at `/transfer` and in the publish
workspace. It sends 8 MB chunks and recovers the confirmed offset after a lost
response, cancellation, page refresh, or server restart. Incomplete sessions
are retained for seven days; completed session receipts are retained for one
day. The original whole-file PUT/POST contract remains available for scripts.

Anonymous upload is disabled by default. To expose an upload-only public page,
set the following values in `config.json` and restart the service:

```json
"anonymous_transfer_upload_enabled": true,
"anonymous_transfer_max_bytes": 2147483648
```

Guests may only create, inspect, and append their own resumable session, bound
to a persistent browser client ID. They cannot list, download, delete, or
overwrite transfer files. A completed guest upload receives an unguessable
per-file sharing page and is automatically deleted with that link after 24
hours. Signed-in uploads also receive per-file sharing pages but do not expire.
The whole-file PUT/POST endpoint remains protected. Expired guest files are
removed by an hourly scheduler job and opportunistically during transfer API
traffic.

API authentication
accepts web session, Basic Auth using `upload_auth`, or Bearer API key with
`file:transfer` (or `admin:*`).

```powershell
curl.exe -u admin:password -T ".\big-file.zip" http://localhost:9998/api/transfer/files/big-file.zip
curl.exe -u admin:password -O http://localhost:9998/api/transfer/files/big-file.zip
curl.exe -u admin:password -X DELETE http://localhost:9998/api/transfer/files/big-file.zip
```

If deployed behind a reverse proxy, configure that proxy to allow large request bodies as well.

### Response Security and HEAD Semantics

All responses carry a same-origin browser security baseline. React routes use a
strict same-origin script policy; the VitePress documentation path separately
allows its generated inline bootstrap scripts. Authentication, admin, transfer,
and operations APIs default to `Cache-Control: no-store`. Session cookies are
HttpOnly with `SameSite=Lax`.

`HEAD` requests are read-only: they never submit a login, delete a transfer
file, create or deliver an operations task, or increment plugin download
statistics. File routes still return the same status and representation headers
as `GET`, without a response body.

Plugin, application, update, tool, transfer, generic storage, and legacy file
URLs share one artifact delivery boundary. It consistently supports validators
and byte ranges and adds `Accept-Ranges: bytes` plus
`X-Content-Type-Options: nosniff`. Plugin download statistics are written only
after the complete body has been iterated. Conditional responses, partial
ranges, and interrupted transfers do not increment the counter; `bytes=0-`
counts when it delivers the complete representation.

Logout state changes use POST. The legacy `GET /logout` URL only redirects to
the public site and does not clear the session. Disabled database-backed users
are rejected on their next authenticated page or API request.

Browser-originated state-changing requests enforce a same-origin CSRF boundary.
Session-authenticated writes additionally require the per-session
`X-CSRF-Token` returned by `GET /api/auth/session`; the token rotates on login
and logout. Headerless native clients and explicit Basic/Bearer API clients keep
their existing contracts, while browser requests with a foreign `Origin` or
cross-site Fetch Metadata are rejected before route execution.

## Disk Scan Points

When indexes are populated, most API requests read from SQLite instead of scanning disk. The following are the remaining real-time disk access points:

### Index-populated (no disk scan)
- `GET /api/plugins` — reads from `plugin_index` table
- `GET /api/plugins/<id>` — reads from `plugin_index` + `package_index`
- `GET /api/site/releases` — reads from `release_index` via `scan_app_release_artifacts`
- `GET /api/site/updates` — reads from `update_index`
- `GET /api/site/tools` — reads from `tool_index`
- `GET /api/site/home` — reads from `release_index`, `update_index`, `tool_index` for previews
- `GET /api/tool/cvwindowsservice/releases` — cached with signature-based invalidation

### Real-time disk access (by design)
- `GET /api/site/browse/<path>?q=<name>&type=all|directory|file&limit=200&offset=0` — reads and filters one live directory before pagination (no recursion). Anonymous callers only see published application, History, Plugins, Spectrum, Update, and Tool artifacts; authenticated administrators retain full storage access.
- `GET /plugins/<id>/icon` — reads icon file for ETag/Last-Modified headers
- `GET /download/<path>` — serves public artifacts directly from disk. Operational storage requires administrator authentication, while Transfer keeps its separate file-transfer authorization policy.
- `GET /api/app/changelog` — reads `CHANGELOG.md` (single file read)
- `GET /api/app/latest-version` — reads in-memory `LATEST_RELEASE` cache (warmed at startup, refreshed on upload)
- `GET /api/android/update` — reads the latest indexed root APK and caches its SHA-256 by path, size, and modification time
- `GET /api/health`, `GET /api/ready` — filesystem probes for liveness

### Scheduler signature checks (lightweight)
- `release_index_check` — two-level History walk (major/branch/file), no deep rglob
- `update_index_check` — single `Update/` directory listing
- `tool_index_check` — single `Tool/` directory listing
- `plugin_index_check` — `plugin_catalog_signature()` over Plugins directory

### Upload/publish (triggers index refresh)
- `POST /api/packages/publish` → `refresh_plugin_index` for that plugin
- `PUT /upload/<path>` → refreshes `release_index`, `update_index`, or `tool_index` based on path
- `POST /api/tool/cvwindowsservice/publish` → refreshes `tool_index`

## New Modules

| Module | Purpose |
|--------|---------|
| `services/auth_middleware.py` | Authentication decorators (Bearer, Basic, session) — single source of truth |
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

```powershell
python -m unittest discover -p "test_*.py"
```

## Existing API Compatibility

All existing API endpoints remain unchanged:
- `GET /api/plugins` — Search plugins (now reads from SQLite index, falls back to disk scan if index is empty)
- `GET /api/plugins/<id>` — Plugin detail (reads from index; fileHash computed on-demand if missing)
- `GET /api/plugins/<id>/latest-version` — Latest version
- `GET /api/packages/<id>/<version>` — Download package
- `POST /api/packages/publish` — Publish (now also supports Bearer auth with `plugin:publish` scope)
- `PUT /upload/<path>` — Legacy upload
- `GET /api/health` — Health check
- `GET /api/ready` — Readiness check
- `GET /api/stats` — Download statistics
- `POST /api/feedback` — Submit feedback

The `/api/plugins/<id>` response structure is fully compatible with the old API: `latestVersion`, `requiresVersion`, `versions` (with `fileHash`), `archivedVersions`, `readme`, `changelog`, `iconUrl`, `totalDownloads`, etc.
