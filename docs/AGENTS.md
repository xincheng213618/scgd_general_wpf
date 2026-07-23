# Documentation guidance

This file adds to the repository-root `AGENTS.md` for the VitePress site under `docs/`.

- Simplified Chinese is the only currently maintained documentation language. Do not restore removed language trees unless the task has an explicit delivery requirement.
- Keep pages aligned with the current source tree and runtime behavior; do not preserve stale module lists or commands merely for historical completeness.
- When adding, moving, or removing a page, update the owning section README and `docs/.vitepress/i18n/navigation-data.json` as needed.
- When retaining a compatibility page for deleted content, follow the existing `redirect_from_deleted_page: true` and `search: false` pattern, and keep navigation pointed at the canonical page.
- Do not hand-edit generated output under `docs/.vitepress/dist/`.
- Run documentation commands from the repository root:

```powershell
# Build and validate the site
npm run docs:build

# Recheck an existing dist without rebuilding
npm run docs:validate:dist
```

- Treat broken links, navigation mismatches, compatibility-entry leaks, and search-index validation failures as documentation build failures.
