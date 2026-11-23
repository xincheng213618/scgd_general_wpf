# AI-first project knowledge

Follow the root knowledge rules. Use `knowledge/maintenance.md` for metadata and change-scoped checks. Use `knowledge/retrieval-checks.md` for search changes or handoff evaluation.

- Keep one topic per independent contract. Write primarily in Simplified Chinese and preserve exact symbols, keys and fields. Cover design reasons, compatibility, operator steps and field diagnosis. Point to source for implementation detail. Keep the structure as short as the contract allows.
- Update facts in place. Remove obsolete steps and fix incoming references. Keep compatibility notes that explain current behavior. Keep shipped README prerequisites and risks available inside the package.
- Map implementation paths in metadata. Keep automated tests with the code. Document field acceptance conditions where they affect the contract. Label planned and historical topics and state their runtime gates. Keep bodies over the `docs:check` budget from growing.
- Put prerequisites, side effects and authorization beside commands. Exclude secrets, private data and unauthorized customer samples. Use local documentation checks for validation. Report routing, source/behavior and delivery evidence separately.
- Generate `knowledge/index.md`, `knowledge/code/`, `knowledge/domains/`, `knowledge/catalog.json` and `.vitepress/i18n/navigation-data.json` from Markdown metadata. Build `.vitepress/dist/` from the same sources.
