# Web

ColorVision marketplace web product boundary. `Frontend/` builds the React
portal and `/admin` SPA into `Frontend/dist`; `Backend/` serves the compiled
site, Flask APIs, downloads and storage/index services.

- [Local startup and NAS deployment](../docs/02-developer-guide/deployment/web.md):
  prerequisites, parameters, build stages, Git bundles, retention and recovery.
- [Web pages and documentation hosting](../docs/02-developer-guide/backend/web-pages.md):
  routes, compression, cache policy and documentation indexes.
- [Backend](../docs/02-developer-guide/backend/README.md): configuration, CLI,
  database ownership and management routes.
- [Web architecture](../docs/03-architecture/components/web.md): responsibilities,
  implemented interfaces, compatibility and build budgets.
- [Historical performance baseline](../docs/02-developer-guide/backend/performance-baseline.md):
  dated measurements and their limits.

## Run locally

Use Windows PowerShell from the repository root with working Node.js, Python
and npm. Prepare `Web/Backend/config.json`, artifact storage and the Backend
SQLite database for the intended environment before starting. `-Storage` only
overrides artifacts; it does not isolate configuration or accounts. The default
Backend host binds all interfaces.

```powershell
# May install dependencies, write build/runtime data, and open a browser.
.\Web\Run-Web.ps1
```

Existing dependencies and documentation output are reused by the local wrapper;
source changes do not necessarily refresh them. Full behavior and skip options
are described in the startup guide linked above.

## Deploy an existing NAS service

`Deploy-Nas.ps1` requires an existing Windows SSH target, checked-out repository,
production configuration/database, runtime executables and scheduled task. It
is not a machine bootstrap script. The default target is `cv-publish`, task is
`\ColorVision\ColorVisionWeb`, and port is `9998`; runtime executable paths are
also fixed in its remote template and must match the target machine.

```powershell
# Read-only deployment inspection over SSH; does not build or restart.
.\Web\Deploy-Nas.ps1 -DryRun
```

Formal deployment changes the remote checkout, dependencies and frontend,
restarts the service, and can remove eligible old backups/bundles. Use the
complete deployment guide before running it. `-SkipTests` still runs frontend
tests. Automatic failure recovery does not restore Git, dependencies or SQLite,
and is conditional on listener/frontend state. Keep configuration/database
backups private and inspect the returned evidence rather than assuming that a
backup or a deployment-history entry proves recovery.

When this directory is delivered separately, use the matching repository's
documentation for the full procedures; relative documentation links require
that checkout.
