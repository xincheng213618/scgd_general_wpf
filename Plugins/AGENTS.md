# Plugin guidance

This file adds to the repository-root `AGENTS.md` for work under `Plugins/`.

- Treat `Plugins/Directory.Build.props`, each plugin `.csproj`, and its `manifest.json` as the current source of truth. Current plugin projects target `net10.0-windows`; add `<UseWPF>true</UseWPF>` only when the plugin has WPF UI.
- Inspect the actual source tree before naming or creating a plugin. Do not recreate historical plugins that are absent from `Plugins/` without an explicit requirement.
- Keep `manifest.json` `id` and `dllpath` valid for the built assembly. Packaging derives the release version from the compiled DLL `FileVersion`; do not treat `manifest.json` `version` as authoritative unless a plugin-specific contract says otherwise. Manifest-based loading is the primary path; the loader's directory-name fallback is compatibility behavior.
- Contribute through the established extension interfaces (menus, initializers, configs, status bar, search, file handlers) rather than wiring directly into the host. `IPlugin`/`IPluginBase` are obsolete and never called. Discover types with `AssemblyHandler.GetImplementationTypes` instead of scanning assemblies, and raise `manifest.json` `requires` when the plugin starts using host APIs newer than the current value.
- Build the changed `.csproj` first. For manifest-only changes, validate with `python .\Scripts\package_cvxp.py --project-file <plugin-csproj> --validate-only`. When the user asks to publish, run `Scripts\package_plugin.bat <PluginName>` from the repository root. The wrapper uploads and removes the local `.cvxp`; do not add or pass `--no-upload`.
- A successful local build is not a successful publish. Verify the remote package metadata and downloadable package before reporting completion.
