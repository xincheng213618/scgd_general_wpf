# Backend guidance

This file adds to the repository-root `AGENTS.md` for the Flask service under `Web/Backend/`.

- Preserve the existing public, admin, API, authentication, artifact-index, and release-metadata contracts. Treat the filesystem artifacts as authoritative where the current code does, with SQLite as an index/cache rather than an invented replacement source of truth.
- Put HTTP routes in `routes/`, authentication/index/storage behavior in `services/`, database migrations in `db/`, and application composition in `app_setup.py` unless the existing architecture provides a more specific owner.
- Do not introduce production credentials or new deployment-only paths in tracked configuration. Use explicit overrides, temporary paths, and secret-free test data.
- Run backend commands from `Web/Backend/`:

```powershell
python -m pip install -r .\requirements.txt
$storage = Join-Path $env:TEMP 'ColorVisionBackend'
python .\app.py --storage $storage
python -m unittest discover -p "test_*.py"
```

- For a focused change, run the closest `unittest` module first, then the full discovery suite when practical. Exercise `/api/health` and `/api/ready` when startup, storage, database, or deployment behavior changes.
- Do not silently relax upload, path, signature, or authorization checks to make a test pass; preserve failure semantics and add explicit fixtures or configuration instead.
