# Web product guidance

This file adds to the repository-root `AGENTS.md` for work under `Web/`.

- `Frontend/` is a React and TypeScript application; its production build is emitted to `Frontend/dist/` and served by the Flask backend.
- When an API contract changes, update the backend tests and the corresponding frontend client/types in the same task.
- Validate frontend changes from `Web/Frontend/` with `npm run lint` and `npm run build`.
- Backend-specific work also follows `Web/Backend/AGENTS.md`.
