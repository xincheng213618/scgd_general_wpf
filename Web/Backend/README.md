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
- [Audit and NAS deployment records, queries, and evidence limits](../../docs/02-developer-guide/backend/management-records.md) (`delivery.backend-records`).
- [Copilot profile management, version proof, and sensitive configuration sync](../../docs/02-developer-guide/backend/copilot-sync.md) (`delivery.backend-copilot-sync`).
- [Public feedback submission, inbox state, and diagnostic attachment delivery](../../docs/02-developer-guide/backend/feedback.md) (`delivery.backend-feedback`).
- [Operations credentials, relay tasks, receipts, and read-only overview](../../docs/02-developer-guide/backend/operations-relay.md) (`delivery.backend-operations`).
- [CVWindowsService publication, latest pointer, package selection, and cache](../../docs/02-developer-guide/backend/cvwindowsservice.md) (`delivery.cvwindowsservice`).

This source-adjacent README retains local prerequisites, module entry points, and operational warnings. The responsibilities above each have one body of knowledge. Repository links require the matching full source checkout; they are not a claim that `docs/` ships with a separately copied backend.

## Run independently

Run from `Web/Backend/` with the dependencies in `requirements.txt`. Prepare
`config.json` from `config.json.example` without overwriting an existing
configuration. Set a private `secret_key`, non-default `upload_auth`, the
intended artifact root, registration policy, host and port. The default host
`0.0.0.0` listens on every interface; use `127.0.0.1` for a local-only service.
Protect the config, database, artifact directories and reverse-proxy transport.

Importing `app` already initializes the Backend-local `marketplace.db`, schema
and caches. `--storage` overrides artifacts only; it does not isolate the
configuration or database. Prepare a separate Backend/config/database for
isolated testing. CLI validation rejects implemented insecure defaults outside
debug mode, but runs after application composition. Starting the service can
write logs, run jobs and listen on the network.

```powershell
# Dependency installation accesses the network.
python -m pip install -r .\requirements.txt
# Start only after preparing and reviewing config.json.
python .\app.py
```

The complete CLI reference, including `--storage`, `--port`, `--debug`, index
refresh, archive reconciliation, pruning, jobs and key creation, is maintained
in [Backend startup and CLI](../../docs/02-developer-guide/backend/README.md#命令行参数).
`GET /api/health` reports liveness; `GET /api/ready` checks basic dependencies
and may create storage directories. Neither proves a full release is usable.

## Maintenance prerequisites

Index refresh rewrites the selected Backend database. Reconciliation moves
artifacts, pruning/retention can delete files or records, and key creation
prints a credential. Check the selected config, database and artifact root,
retain recovery access, and authorize the intended operation before using the
CLI or management pages. Do not run maintenance or publishing as a connection
test. File transfer additionally needs appropriate proxy body/timeout limits
and a reviewed destination/overwrite policy.

Detailed commands and failure semantics live in the topics above. The admin
SPA is served under `/admin`; access depends on the endpoint's current role,
credential and scope checks.

## Implementation entry points

- `app_setup.py`, `context.py`, `app.py`: composition, dependencies and the compatibility application shell.
- `cli.py`, `config_loader.py`: command parsing and configuration.
- `routes/`, `marketplace_api_routes.py`: HTTP adapters.
- `services/`, `ports/`, `db/repositories/`, `db/schema_version.py`: policies, interfaces, persistence and migrations.
- `routes/frontend_spa.py`: compiled `../Frontend/dist` hosting and admin gate.

## Tests

Inspect the selected tests' config/database isolation before execution. A
temporary artifact root alone is insufficient; documentation changes do not
require starting the service.

```powershell
python -m unittest discover -p "test_*.py"
```
