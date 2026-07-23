# Web

ColorVision marketplace web product boundary.

Architecture boundaries, compatibility policy, extension ports, analytics
privacy rules, and performance guardrails are documented in
[`ARCHITECTURE.md`](ARCHITECTURE.md).
The measured 2026-07-18 baseline, completed first pass, and prioritized follow-up
work are recorded in [`PERFORMANCE_AUDIT.md`](PERFORMANCE_AUDIT.md).

```text
Web/
├── Frontend/   # React + TypeScript + Ant Design + ProComponents
└── Backend/    # Flask APIs, auth, storage/index services
```

`Frontend` builds the public portal and `/admin` management SPA into
`Frontend/dist`. `Backend` serves the compiled React app, JSON APIs, auth,
downloads, and storage/index services.

Common commands:

```powershell
.\Web\Run-Web.bat

# Deploy the latest develop branch to the NAS and verify the live service.
.\Web\Deploy-Nas.bat

# Inspect the pending NAS update without changing production files.
.\Web\Deploy-Nas.ps1 -DryRun

# Rebuild and restart even when the NAS commit is already current.
.\Web\Deploy-Nas.ps1 -Force

cd Web/Frontend
npm install
npm run build

cd ../Backend
python app.py --port 9998
```

`Deploy-Nas.ps1` defaults to the SSH alias `cv-publish`, the production task
`\ColorVision\ColorVisionWeb`, and port `9998`. Override `-SshTarget`,
`-RepoPath`, `-StoragePath`, `-TaskPath`, `-TaskName`, or `-Port` when another
host uses a different layout. Each deployment preserves the production config,
SQLite database, and previous frontend build under
`D:\ColorVision\web-deploy-backups`, then appends the result to
`D:\ColorVision\web-deploy-history.jsonl`.
