# Frontend

React + TypeScript + Ant Design + ProComponents frontend for the public portal
and `/admin` management system.

## Commands

```powershell
npm install
npm run dev
npm run lint
npm run build
```

The production build is mounted by Flask from `Web/Frontend/dist/`. Public
routes are served at `/`, `/plugins`, `/releases`, `/updates`, `/tools`, and
`/browse`; management routes live under `/admin`.

## Structure

```text
src/
├── layouts/       # ProLayout shell
├── pages/         # ProTable / ProForm / ProDescriptions pages
├── services/      # API clients
└── types/         # TypeScript interfaces
```
