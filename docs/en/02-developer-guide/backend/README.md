# Plugin Marketplace Backend

The ColorVision Plugin Marketplace Backend is a lightweight service based on Python Flask for managing plugin publishing, downloading, and version control.

## Feature Overview

The backend service provides the following core features:

- **Web Management Interface** - Browse, search, download, and upload plugins
- **REST API** - Provides interfaces for the WPF desktop client
- **Legacy Compatibility** - Supports compatibility routes for older client versions
- **Download Statistics** - SQLite-based download statistics

## Project Structure

```
Backend/marketplace/
├── app.py              # Flask app main entry (Web UI + API + legacy compatibility)
├── app_changelog.py    # Changelog management module
├── app_releases.py     # App version release management
├── catalog_view_models.py  # Plugin catalog view models
├── config.json         # Configuration file
├── download_stats.py   # Download statistics module
├── feedback_service.py # User feedback service
├── marketplace.db      # SQLite database (auto-created, gitignored)
├── marketplace_services.py # Marketplace data service
├── package_publish.py  # Package publishing validation and processing
├── page_contexts.py    # Page context builder
├── plugin_marketplace.py   # Plugin marketplace core logic
├── plugin_queries.py   # Plugin query interface
├── requirements.txt    # Python dependencies
├── runtime_health.py   # Runtime health check
├── storage_browser.py  # Storage browser
├── storage_paths.py    # Storage path management
├── storage_uploads.py  # Upload processing
├── update_retention.py # Update package retention policy
├── static/             # Static assets
└── templates/          # Jinja2 template files
    ├── base.html
    ├── index.html
    ├── plugins.html
    ├── plugin_detail.html
    ├── upload.html
    └── browse.html
```

## Installation and Running

### Environment Requirements

- Python 3.9+
- pip

### Install Dependencies

```bash
cd Backend/marketplace
pip install -r requirements.txt
```

### Configuration File

Edit `config.json`:

```json
{
    "storage_path": "H:\\ColorVision",
    "host": "0.0.0.0",
    "port": 9998,
    "debug": false,
    "secret_key": "your-secret-key",
    "app_release_keep_count": 5,
    "plugin_package_keep_count": 3,
    "upload_auth": {
        "username": "admin",
        "password": "admin"
    }
}
```

Configuration item descriptions:

| Config Item | Description | Default |
|--------|------|--------|
| `storage_path` | Plugin and app storage path | `storage/` |
| `host` | Listen address | `0.0.0.0` |
| `port` | Listen port | `9998` |
| `debug` | Debug mode | `false` |
| `secret_key` | Flask secret key | Must be changed |
| `upload_auth` | Upload authentication credentials | Must be changed |

### Start Service

```bash
# Use default configuration
python app.py

# Specify storage path
python app.py --storage H:\ColorVision

# Specify port
python app.py --port 9999
```

## API Interfaces

### Web UI Routes

| Route | Function |
|------|------|
| `GET /` | Home — Storage overview, quick links |
| `GET /plugins` | Plugin marketplace — Search, categories, sorting |
| `GET /plugins/{id}` | Plugin details — Version list, README, download |
| `GET /upload` | Upload page |
| `POST /upload` | Process upload |
| `GET /browse[/path]` | File browser |
| `GET /releases` | Release version list |
| `GET /updates` | Update package list |
| `GET /tools` | Tool download list |

### REST API

| Method | Path | Description |
|------|------|------|
| GET | `/api/plugins` | Search plugins (keyword, category, sort, pagination) |
| GET | `/api/plugins/{id}` | Plugin details + all versions |
| GET | `/api/plugins/{id}/latest-version` | Plain text latest version |
| POST | `/api/plugins/batch-version-check` | Batch version check |
| GET | `/api/plugins/categories` | Get all categories |
| GET | `/api/packages/{id}/{version}` | Download plugin package |
| POST | `/api/packages/publish` | Publish new version (Basic Auth required) |
| GET | `/api/stats` | Download statistics |
| GET | `/api/health` | Health check endpoint |
| GET | `/api/ready` | Readiness check endpoint |

### Legacy Compatibility Routes

| Route Pattern | Description |
|----------|------|
| `PUT /upload/{path}` | Compatible with old build script uploads |
| `/D%3A/ColorVision/Plugins/{path}` | Compatible with old client version check and download |

## Authentication

The upload interface is protected with HTTP Basic Auth:

```bash
# curl example
curl -u username:password -X POST http://localhost:9998/api/packages/publish \
  -F "PluginId=Spectrum" \
  -F "Version=1.0.0.1" \
  -F "package=@Spectrum-1.0.0.1.cvxp"
```

## Storage Structure

The backend directly uses the existing file system structure:

```
{storage_path}/
├── LATEST_RELEASE              # App latest version number
├── CHANGELOG.md                # App changelog
├── History/                    # Historical full installers
├── Update/                     # Incremental update packages
├── Plugins/                    # Plugin directory
│   ├── Spectrum/
│   │   ├── LATEST_RELEASE
│   │   ├── manifest.json
│   │   ├── PackageIcon.png
│   │   ├── README.md
│   │   ├── CHANGELOG.md
│   │   └── Spectrum-1.0.0.1.cvxp
│   └── ...
└── Tool/                       # Tool downloads
```

## Testing

The backend includes a complete test suite:

```bash
# Run all tests
python -m pytest

# Run specific test files
python test_app.py
python test_app_releases.py
python test_page_contexts.py
python test_upload_services.py
```

## Integration with Build Scripts

The backend integrates with build scripts in the `Scripts/` directory:

- `publish_plugin.py` - Publish plugins using `/api/packages/publish`
- `build.py` - Upload main program installer
- `build_update.py` - Upload incremental update packages
- `build_spectrum.py` - Upload Spectrum plugin

## Tech Stack

| Layer | Selection | Version |
|------|------|------|
| Language | Python | 3.9+ |
| Framework | Flask | >=3.0 |
| Template Engine | Jinja2 | Built-in |
| CSS Framework | Bootstrap 5 | 5.x |
| Database | SQLite | Built-in |
| Markdown Rendering | markdown | >=3.8 |

## Access Addresses

After service starts:

- Web UI: http://localhost:9998
- Plugin Marketplace: http://localhost:9998/plugins
- API: http://localhost:9998/api/plugins
- File Browser: http://localhost:9998/browse

## Deployment Recommendations

### Production Deployment

1. **Use Gunicorn/uWSGI**

```bash
pip install gunicorn
gunicorn -w 4 -b 0.0.0.0:9998 app:app
```

2. **Nginx Reverse Proxy**

```nginx
server {
    listen 80;
    server_name marketplace.example.com;

    location / {
        proxy_pass http://localhost:9998;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

3. **Enable HTTPS**

Use Let's Encrypt or self-signed certificates to enable HTTPS.

4. **Monitoring and Logging**

- Configure log rotation
- Set up monitoring alerts
- Regularly check disk space

## Troubleshooting

### Service Won't Start

Check if the port is occupied:
```bash
netstat -an | findstr 9998
```

### Upload Failure

- Confirm `upload_auth` configuration is correct
- Check storage path permissions
- View log error messages

### Database Error

Delete the auto-generated `marketplace.db` file; it will be automatically recreated after restarting the service.