# ColorVision Marketplace Backend

Flask-based backend serving the ColorVision plugin marketplace, update distribution, and internal management portal.

Authoritative documentation is split by responsibility:

- [Backend composition, configuration, authentication boundary, and health/readiness](../../docs/02-developer-guide/backend/README.md) (`delivery.backend`).
- [File transfer, overwrite, public sharing, and expiry](../../docs/02-developer-guide/backend/file-transfer.md) (`delivery.file-transfer`).
- [Plugin catalog, response projections, index refresh, and version cache](../../docs/02-developer-guide/backend/plugin-catalog.md) (`delivery.plugin-catalog`).
- [Web accounts, roles, passwords, and sessions](../../docs/02-developer-guide/backend/accounts.md) (`delivery.backend-accounts`).
- [HTTP credentials, API keys, and browser CSRF](../../docs/02-developer-guide/backend/authentication.md) (`delivery.backend-auth`).
- [HTTP artifacts, completion counts, cache, HEAD, and compression](../../docs/02-developer-guide/backend/artifact-delivery.md) (`delivery.artifact-delivery`).
- [Public site projections, archives, Android metadata, and storage browsing](../../docs/02-developer-guide/backend/public-data.md) (`delivery.backend-public-data`).
- [HTTP/SPA analytics, privacy boundaries, and performance diagnostics](../../docs/02-developer-guide/backend/observability.md) (`delivery.backend-observability`).
- [Built-in jobs, synchronous execution, single-flight, and recovery](../../docs/02-developer-guide/backend/jobs.md) (`delivery.backend-jobs`).
- [Live retention settings, database snapshots, cleanup, and rotation](../../docs/02-developer-guide/backend/backup-retention.md) (`delivery.backend-retention`).

This source-adjacent README retains local prerequisites and the remaining Copilot, Operations, feedback, deployment-history, audit-query, and separate CVWindowsService details not yet moved to canonical topics. The responsibilities above each have one body of knowledge. Repository links require the matching full source checkout; they are not a claim that `docs/` ships with a separately copied backend.

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

### Plugin Index

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/admin/index/plugins/refresh` | Refresh all plugin indexes |
| POST | `/api/admin/index/plugins/<id>/refresh` | Refresh single plugin index |

### Scheduled maintenance and retention

The [job contract](../../docs/02-developer-guide/backend/jobs.md) owns definitions,
initial intervals, thread startup, synchronous manual runs, enable/disable,
single-flight, recovery, and execution history. The [retention contract](../../docs/02-developer-guide/backend/backup-retention.md)
owns the six live settings and database snapshot creation, privacy cleanup, and
rotation; keep endpoint/limit tables and failure semantics in those topics.

Running a job may delete records or files. Disabling it does not cancel an
in-flight handler. A successful HTTP response or a snapshot filename does not
prove every phase, history write, or old-file cleanup succeeded. Confirm the
actual database/configuration and recovery arrangements before maintenance.

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

Audit cutoff, recognized snapshots, partial failures, and rotation are defined
in the [retention contract](../../docs/02-developer-guide/backend/backup-retention.md).
Do not assume an unrecognized copy or a failed cleanup has applied that policy.
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

### Observability and database maintenance

The canonical [observability topic](../../docs/02-developer-guide/backend/observability.md)
owns HTTP/SPA/visitor-day definitions, endpoint parameters, response-size and
timing boundaries, asynchronous queue failures, daily HMAC limits, reporting
calendar metadata, performance samples, and analytics configuration.

HTTP access, SPA events, plugin completion counts, and slow-request logs have
different data and privacy boundaries. Daily keys are not a guarantee of
untraceable people; a 202 event response is not a persistence receipt.

Database snapshot creation, normal response fields, retention counts,
transient-account-state removal, and failure handling belong to the
[backup/retention topic](../../docs/02-developer-guide/backend/backup-retention.md).
A scrubbed database is not a backup of artifacts or configuration, does not
revoke every kind of credential, and does not replace a recovery exercise.

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

## Deployment Notes

The following actions can initialize or modify the selected Backend database,
configuration, and artifact state; backups/retention may also remove retained
files. Confirm the target and authority before running them. See Quick Start
for the non-isolating `--storage` boundary.

1. **First deploy**: Run `python app.py --refresh-all-indexes` to populate all indexes (plugins, releases, updates, tools).
2. **Manual file changes**: If you manually modify storage directories, either:
   - Wait for the periodic scheduler check, or
   - Call `POST /api/admin/index/refresh-all`
3. **Database backup**: See the [backup/retention contract](../../docs/02-developer-guide/backend/backup-retention.md) before calling `POST /api/admin/backup/db`; creation can also clean live audit data and modify or rotate existing snapshots.
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

## Public reads and separate tool releases

[Public data](../../docs/02-developer-guide/backend/public-data.md) owns home,
Windows/Android archives, changelog, update/tool pages, and browse filtering.
It distinguishes indexed SQL paging from legacy fallback, page counts from
whole-directory counts, and GET cache writes/storage repair from file serving.
Do not infer no I/O or no writes from the HTTP method or a compact projection.

`GET /api/tool/cvwindowsservice/releases` remains a separate tool release
metadata path with signature-based caching, owned by `routes/cvws_api.py`.
It is not the generic `/api/site/tools` listing. Its broader publish/download
contract has not been consolidated into the public-data topic.
`POST /api/tool/cvwindowsservice/publish` calls `on_storage_change` with
`Tool/CVWindowsService` after saving, dispatching a tool_index refresh;
this is not an atomic file/index completion guarantee.

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
  "scheduler_enabled": true
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
