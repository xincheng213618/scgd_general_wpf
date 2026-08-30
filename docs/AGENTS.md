# AI-first project knowledge

This file supplements the repository-root `AGENTS.md`. `docs/` is the project's versioned knowledge layer for AI-assisted work; VitePress is a derived reading surface.

- Start with the compact `knowledge/index.md` and only the relevant generated source map, or use the local search command. Capability domain maps are secondary lookup views. Then read the smallest relevant set of topics and linked code/tests. Do not require a website build, external MCP service, personal memory, or a previous conversation to answer repository questions.
- Maintain one canonical body of topic knowledge, primarily in Simplified Chinese. Keep `AGENTS.md` in English and preserve useful original-language module/package READMEs; this is not a repository-wide translation rule. Preserve exact symbols, configuration keys, protocol fields and search aliases. Do not maintain translated mirrors without a specific delivery requirement.
- Every active Markdown page has the knowledge frontmatter described in `knowledge/maintenance.md`. Source paths are repository-relative and must resolve to real files/directories; a test reference is not a claim that the test has been run.
- Organize around actual code responsibilities and cross-module execution chains, not audiences such as users, developers, or maintainers. Keep a capability's observable behavior, implementation contract, failure diagnosis and verification together when they describe the same boundary. Existing numbered paths are stable addresses, not a required reader journey. Source maps and website navigation derive from the topic catalog and `code_paths`; do not hand-maintain a second navigation tree or copy the source-file tree into prose.
- Write independently understandable topics: scope, current behavior, non-obvious rationale/invariants, implementation entry points and verification. Split by semantic boundary, not a fixed line count. Merge repetition by linking to the canonical topic rather than copying contracts.
- Keep source-adjacent READMEs as module/package entry points, linking directly to current topics rather than retired pages. Preserve package-local prerequisites and warnings when a README is shipped without `docs/`; a repository-relative link does not make the knowledge available inside the package.
- Distinguish `current`, `planned`, and `historical`. Keep proposed behavior out of current capability claims. Mark uncertainty and verification gaps explicitly; do not add a fabricated verification date or green result.
- Put command prerequisites, local/external side effects and authorization requirements beside the command. Packaging wrappers may upload; never execute them merely to validate a documentation change.
- When code behavior or a command changes, use `knowledge.mjs impact <path>` and update affected topics in the same change. When retiring a topic, remove it from active knowledge; retain a `redirect_from_deleted_page: true` / `search: false` page only when its old URL has compatibility value.
- Do not hand-edit `knowledge/index.md`, `knowledge/code/*.md`, `knowledge/domains/*.md`, `knowledge/catalog.json`, or `.vitepress/dist/`. Generate the maps and catalog from Markdown metadata, and build the website from the same sources.

Run from the repository root:

```powershell
# Local knowledge generation; no website dependencies required for the Node CLI
npm run docs:knowledge
# Metadata, source/test paths, README-to-docs pointers, generated freshness and tooling tests
npm run docs:check
# Website, links, generated search and compatibility routes
npm run docs:build
```

Use `knowledge/retrieval-checks.md` for clean-context question probes. Passing schema/link tests does not prove every statement or every AI answer correct. Preserve that distinction in completion reports.
