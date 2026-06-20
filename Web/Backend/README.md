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

## Quick Start

```bash
pip install -r requirements.txt
python app.py                        # uses config.json
python app.py --storage /path/to/dir # override storage path
python app.py --port 8080            # override port
python app.py --debug                # debug mode
```

### Index Management

```bash
# Refresh the full plugin index
python app.py --refresh-index

# Refresh a single plugin's index
python app.py --refresh-plugin-index MyPlugin
```

## Admin API

All admin endpoints require authentication (session login or Basic Auth or Bearer API Key).

Session and Basic Auth always have full access. Bearer API Key access is controlled by per-endpoint scopes:

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
| POST `/api/admin/backup/db` | `admin:*` |
| GET `/api/admin/jobs` | `jobs:read` |
| POST `/api/admin/jobs/<id>/run` | `jobs:write` |
| POST `/api/admin/jobs/<id>/enable` | `jobs:write` |
| POST `/api/admin/jobs/<id>/disable` | `jobs:write` |
| GET `/api/admin/stats/overview` | `stats:read` |
| GET `/api/admin/audit-log` | `admin:*` |
| API Key management | `admin:*` |

`admin:*` grants access to all endpoints. Session and Basic Auth (validated against `upload_auth` config) always have full access.

### Cache Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/cache/status` | Database and cache status |
| POST | `/api/admin/cache/cleanup` | Delete expired cache entries |

### Plugin Index

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/admin/index/plugins/refresh` | Refresh all plugin indexes |
| POST | `/api/admin/index/plugins/<id>/refresh` | Refresh single plugin index |

### Jobs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/jobs` | List scheduled jobs |
| POST | `/api/admin/jobs/<id>/run` | Run job immediately |
| POST | `/api/admin/jobs/<id>/enable` | Enable job |
| POST | `/api/admin/jobs/<id>/disable` | Disable job |

### API Keys

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/api-keys` | List API keys |
| POST | `/api/admin/api-keys` | Create new key (returns plaintext once) |
| POST | `/api/admin/api-keys/<id>/revoke` | Revoke key |
| POST | `/api/admin/api-keys/<id>/rotate` | Rotate key (revoke old, create new) |
| GET | `/api/admin/api-keys/<id>/usage` | Get key usage info |

### Audit Log

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/audit-log?action=&limit=&offset=` | View audit log |

### Stats

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/stats/overview` | Download and index statistics |

## Admin Pages

| Path | Description |
|------|-------------|
| `/admin/` | Overview dashboard |
| `/admin/cache` | Cache and index management |
| `/admin/api-keys` | API Key lifecycle management |
| `/admin/jobs` | Scheduled job management |
| `/admin/audit` | Audit log viewer |

## API Key Authentication

### Creating a Key

```bash
curl -X POST http://localhost:9998/api/admin/api-keys \
  -u admin:password \
  -H "Content-Type: application/json" \
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

```bash
curl -X POST http://localhost:9998/api/packages/publish \
  -H "Authorization: Bearer cvmp_a1b2c3d4_e5f6g7h8i9j0..." \
  -F "PluginId=MyPlugin" \
  -F "Version=1.0.0" \
  -F "package=@MyPlugin-1.0.0.cvxp"
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
| `startup_index_check` | Once | Ensure all indexes are populated on startup |

The scheduler starts automatically when `scheduler_enabled` is true (default). In debug mode, it only starts in the Flask reloader child process to avoid duplicate threads. Set `scheduler_enabled: false` in config.json to disable.

Signature-based check: each index check computes a directory signature and compares it with the stored signature in `index_state`. If they match, no refresh is triggered. The signature is updated after each successful refresh.

## Deployment Notes

1. **First deploy**: Run `python app.py --refresh-all-indexes` to populate all indexes (plugins, releases, updates, tools).
2. **Manual file changes**: If you manually modify storage directories, either:
   - Wait for the periodic scheduler check, or
   - Call `POST /api/admin/index/refresh-all`
3. **Database backup**: `POST /api/admin/backup/db` creates a timestamped backup of `marketplace.db`.
4. **API Key security**: Keys are shown only once at creation. Revoke and rotate if compromised. Scopes are validated against a whitelist at creation time.
5. **Config**: Edit `config.json` to set `storage_path`, `upload_auth`, `secret_key`, and scheduler settings.

### Large File Transfer

The protected transfer area is configured by `transfer_upload_dir` (default: `Transfer`, relative to `storage_path`). It is intentionally limited to files directly inside that folder; subdirectories and path traversal are rejected.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/transfer/files` | List transfer files |
| PUT/POST | `/api/transfer/files/<filename>` | Stream-upload a file without the package upload size limit |
| GET | `/api/transfer/files/<filename>` | Download a transfer file |
| DELETE | `/api/transfer/files/<filename>` | Delete a transfer file |

The React admin UI exposes this workflow at `/admin/files`. API authentication
accepts web session, Basic Auth using `upload_auth`, or Bearer API key with
`file:transfer` (or `admin:*`).

```bash
curl -u admin:password -T "big-file.zip" http://localhost:9998/api/transfer/files/big-file.zip
curl -u admin:password -O http://localhost:9998/api/transfer/files/big-file.zip
curl -u admin:password -X DELETE http://localhost:9998/api/transfer/files/big-file.zip
```

If deployed behind a reverse proxy, configure that proxy to allow large request bodies as well.

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
- `GET /api/site/browse/<path>` — always reads live directory listing (small directory, no recursion)
- `GET /plugins/<id>/icon` — reads icon file for ETag/Last-Modified headers
- `GET /download/<path>` — serves file directly from disk
- `GET /api/app/changelog` — reads `CHANGELOG.md` (single file read)
- `GET /api/app/latest-version` — reads in-memory `LATEST_RELEASE` cache (warmed at startup, refreshed on upload)
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
| `routes/admin_api.py` | Admin REST API (cache, index, jobs, audit, keys, perf) |
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

```bash
python -m pytest Web/Backend
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
- `GET /api/feedback` — Submit feedback

The `/api/plugins/<id>` response structure is fully compatible with the old API: `latestVersion`, `requiresVersion`, `versions` (with `fileHash`), `archivedVersions`, `readme`, `changelog`, `iconUrl`, `totalDownloads`, etc.
