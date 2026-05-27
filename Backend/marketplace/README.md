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
- `cache:read` — Read cache status
- `cache:refresh` — Refresh caches
- `stats:read` — Read statistics
- `jobs:read` — Read job status
- `jobs:write` — Run/enable/disable jobs
- `admin:*` — Full admin access

## Scheduled Jobs

| Job | Interval | Description |
|-----|----------|-------------|
| `plugin_index_check` | 5 min | Check Plugins directory signature, refresh if changed |
| `cache_cleanup` | 1 hour | Delete expired cache entries |
| `startup_index_check` | Once | Ensure plugin_index is populated on startup |

## Deployment Notes

1. **First deploy**: Run `python app.py --refresh-index` to populate the plugin index.
2. **Manual file changes**: If you manually modify `H:\ColorVision\Plugins`, either:
   - Wait for the 5-minute periodic check, or
   - Call `POST /api/admin/index/plugins/refresh`
3. **Database backup**: `marketplace.db` contains indexes, cache, and audit logs. Back it up.
4. **API Key security**: Keys are shown only once at creation. Revoke and rotate if compromised.
5. **Config**: Edit `config.json` to set `storage_path`, `upload_auth`, `secret_key`, and scheduler settings.

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
python -m pytest Backend/marketplace
```

## Existing API Compatibility

All existing API endpoints remain unchanged:
- `GET /api/plugins` — Search plugins (now reads from index)
- `GET /api/plugins/<id>` — Plugin detail
- `GET /api/plugins/<id>/latest-version` — Latest version
- `GET /api/packages/<id>/<version>` — Download package
- `POST /api/packages/publish` — Publish (now also supports Bearer auth)
- `PUT /upload/<path>` — Legacy upload
- `GET /api/health` — Health check
- `GET /api/ready` — Readiness check
- `GET /api/stats` — Download statistics
- `GET /api/feedback` — Submit feedback
