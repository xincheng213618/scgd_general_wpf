# Customer-project guidance

This file adds to the repository-root `AGENTS.md` for work under `Projects/`.

- Customer bundles are independently versioned products. Inspect the bundle `.csproj` plus its manifest, README, and closest tests when present before changing behavior or release metadata.
- Keep shared platform code in `Engine/` or `UI/` when it is genuinely reusable; keep customer-specific workflows and configuration in the owning `Project*` bundle.
- Build and test the affected bundle directly. Do not assume the main application's version is also the bundle version.
- When the user asks to publish a customer bundle, run `Scripts\package_project.bat <ProjectName>` from the repository root. The wrapper always uploads and removes the local `.cvxp`; `--no-upload` is not supported.
- Verify remote metadata and a downloadable artifact before reporting a publish as complete.
