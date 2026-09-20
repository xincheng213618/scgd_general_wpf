# AI-first project knowledge

This file supplements the repository-root `AGENTS.md`. `docs/` is the project's versioned knowledge layer for AI-assisted work; VitePress is a derived reading surface.

- For unfamiliar ownership, use `knowledge/index.md`, a relevant source map or local search. When the owning topic is known, read it and the necessary code/tests directly. If natural-language search misses, use a real UI name, symbol, source path or `rg`; do not keep broadening the reading set. Repository questions need no website build, external MCP service, personal memory or previous conversation.
- Maintain one canonical body of topic knowledge, primarily in Simplified Chinese. Keep `AGENTS.md` in English and preserve useful original-language module/package READMEs. Preserve exact symbols, configuration keys and protocol fields; use real feature/UI names, APIs and diagnostic terms as search aliases so questions resolve to their owning topic. Do not maintain translated mirrors without a specific delivery requirement.
- Every active Markdown page has the knowledge frontmatter described in `knowledge/maintenance.md`. Source paths are repository-relative and must resolve to real files/directories; a test reference is not a claim that the test has been run.
- Organize around actual code responsibilities and cross-module execution chains, not audiences such as users, developers, or maintainers. Keep a capability's observable behavior, implementation contract, failure diagnosis and verification together when they describe the same boundary. Existing numbered paths are stable addresses, not a required reader journey. Source maps and website navigation derive from the topic catalog and `code_paths`; do not hand-maintain a second navigation tree or copy the source-file tree into prose.
- Maintain durable knowledge in place: purpose, design reasons, observable contracts, non-obvious invariants, compatibility, operation prerequisites and verification. Keep local implementation explanations near their code; do not create a website topic for every class or repeat mechanically derivable field lists. Replace outdated facts and remove redundant explanations. Change narratives and per-run evidence belong in Git/CHANGELOG or task reports; link to the canonical topic instead of repeating its contract.
- Use a Microsoft Learn-style product-document structure: lead with purpose and applicability; use prerequisites, steps and expected results for procedures, and focused behavior/parameter tables for references. Keep implementation evidence with its topic and use clear, descriptive headings. Prioritize clarity, accuracy, findability and absence of redundancy; length is not a success metric. Merge duplicated explanations while retaining the steps, examples, defaults, constraints and search terms needed to understand and use the feature.
- Keep source-adjacent READMEs as module/package entry points, linking directly to current topics rather than retired pages. Preserve package-local prerequisites and warnings when a README is shipped without `docs/`; a repository-relative link does not make the knowledge available inside the package.
- Distinguish `current`, `planned`, and `historical`. Keep proposed behavior out of current capability claims. Mark uncertainty and verification gaps explicitly; do not add a fabricated verification date or green result.
- Put command prerequisites, local/external side effects and authorization requirements beside the command. Packaging wrappers may upload; never execute them merely to validate a documentation change.
- Apply the root `AGENTS.md` documentation triggers and the change-scoped validation table in `knowledge/maintenance.md`. Internal refactoring or restoring an unchanged contract needs no prose update when existing knowledge, implementation entry points and verification guidance remain accurate; correct affected statements or references otherwise. Use `impact` for unclear ownership or moved/deleted references. When retiring a topic, retain a `redirect_from_deleted_page: true` / `search: false` page only when its old URL has compatibility value.
- Do not hand-edit `knowledge/index.md`, `knowledge/code/*.md`, `knowledge/domains/*.md`, `knowledge/catalog.json`, or `.vitepress/dist/`. Generate the maps and catalog from Markdown metadata, and build the website from the same sources.

Run from the repository root:

```powershell
# Only when catalog inputs change; no website dependencies required
npm run docs:knowledge
# Metadata, source/test paths, README-to-docs pointers, generated freshness and tooling tests
npm run docs:check
# Local site verification when the change requires it; documentation CI also runs this
npm run docs:build
```

Use `knowledge/retrieval-checks.md` for clean-context question probes. Passing schema/link tests does not prove every statement or every AI answer correct. Preserve that distinction in completion reports.
