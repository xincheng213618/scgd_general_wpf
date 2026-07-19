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

cd Web/Frontend
npm install
npm run build

cd ../Backend
python app.py --port 9998
```
