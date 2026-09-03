# Frontend

React + TypeScript + Ant Design + ProComponents frontend for the public portal,
account pages and `/admin` management system.

[Web pages and documentation hosting](../../docs/02-developer-guide/backend/web-pages.md)
maintains application routes, development proxy behavior, static compression,
cache policy, deployment chunk recovery and documentation hosting.
[Web architecture](../../docs/03-architecture/components/web.md) covers module
responsibilities and the build budget.

## Develop locally

Run the following from `Web/Frontend/` with Node.js and npm compatible with the
project dependencies. Installation accesses the network and updates local
node_modules. The tests use Node's TypeScript stripping support.

```powershell
npm install
npm run dev
```

Start the separately configured Flask backend on port 9998 before using API
features. Vite proxies only `/api` to `http://127.0.0.1:9998` with
`changeOrigin=false`; it does not proxy login/logout, documentation or arbitrary
download paths. Backend startup can write its database, logs and artifacts;
use the [Backend prerequisites](../../docs/02-developer-guide/backend/README.md)
for the intended environment. A temporary artifact directory is not database
or account isolation.

## Validate and build

Run each required check and resolve failures before continuing:

```powershell
npm run lint
npm run test
npm run build
```

The build writes `dist/`, including the Vite manifest and verified precompressed
static files, and checks the management entry bundle budget. Flask serves this
production directory. `npm run preview` is a Vite preview, not verification of
Flask authentication, static-file negotiation or deployment recovery.

Source entry points are `src/App.tsx` for routes, `src/layouts/` and `src/pages/`
for views, `src/services/` for API clients, and `src/types/` / `src/utils/` for
contracts and shared behavior. For full local launch or an existing NAS service,
see [Web startup and deployment](../../docs/02-developer-guide/deployment/web.md).
Use the matching repository for these links when the frontend is delivered alone.
