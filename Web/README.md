# Web

ColorVision marketplace web product boundary.

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
cd Web/Frontend
npm install
npm run build

cd ../Backend
python app.py --port 9998
```
