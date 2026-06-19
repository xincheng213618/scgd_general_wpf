# ColorVision.Solution

This page only retains the most important and stable entry point types and sub-modules currently in `UI/ColorVision.Solution/`, no longer continuing the old documentation's "complete API whitepaper + version list + comprehensive RBAC takeover" style.

## Module Positioning

`ColorVision.Solution` is currently more appropriately understood as the desktop workspace shell, rather than simply a "solution manager."

What it now actually handles includes:

- Creation, opening, and recent file management of `.cvsln` solutions
- Left-side tree-based project browsing and new item entry points
- File/folder editor selection and opening
- AvalonDock document area and panel layout management
- Built-in terminal control
- Multi-image viewer and thumbnail cache
- Markdown preview
- Solution-side local RBAC sub-module

This means it is neither a single window nor a very thin UI layer organized solely around `SolutionManager`.

## Most Critical Directories

From the project directory, the most worthwhile to recognize first are:

- `Editor/`: File and folder editor registration, selection, and opening
- `Explorer/`: Solution tree, node models, new items, and context menus
- `Workspace/`: AvalonDock document area and panel layout management
- `Terminal/`: Built-in terminal control and ConPTY wrapper
- `MultiImageViewer/`: Folder multi-image preview and thumbnail cache
- `RecentFile/`: Recent file history
- `Rbac/`: Solution-side local users, roles, permissions, sessions, and audit module
- Root-level `SolutionManager.cs`: Solution open, create, and current workspace switch entry point

## Key Entry Point Types

### `SolutionManager`

`SolutionManager` is the central object for the current workspace entry point. It is responsible for:

- Opening or creating `.cvsln`
- Maintaining the list of recently opened solutions
- Generating the current `SolutionExplorer`
- Deciding which solution to open at startup based on command line or recent files

If tracing "how does the solution come in," you typically look at it first rather than the tree control.

### `SolutionExplorer`

`SolutionExplorer` and the node types in the `Explorer/` directory work together to organize directories, files, new items, and right-click actions into a tree workspace.

This part is the main entry point for "how the user sees the project structure."

### `EditorManager`

`EditorManager` is responsible for editor registration and dispatch. The current implementation characteristics are very clear:

- Scans loaded assemblies for types implementing `IEditor`
- Registers via `EditorForExtensionAttribute`, `GenericEditorAttribute`, `FolderEditorAttribute`
- Allows configuring default editors for extensions
- Also supports folder editors

So the current editor system is not a hand-written switch table, but an attribute-driven registration mechanism.

### `WorkspaceManager` and `DockLayoutManager`

These two handle docking and recovery of the current document workspace:

- Find and activate the current document
- Maintain `ContentId` and document selection state
- Save and load AvalonDock layouts
- Restore panels and document content by registry during layout recovery

If the problem manifests as "where did the tab go," "layout not recovered," "document area lost," typically look at this chain first.

### `TerminalControl`

Terminal capability is right in this project, not a separate external service. `TerminalControl` is currently responsible for:

- Starting `cmd` or `powershell`
- Receiving ConPTY output
- Maintaining screen buffer and command history
- Running scripts and handling URL clicks

So it is closer to a built-in terminal UI component, rather than merely "calling the system terminal."

### `MultiImageViewer`

`MultiImageViewer` can be used both as a standalone `UserControl` and connected to the editor system via `MultiImageViewerEditor`.

It is currently primarily responsible for:

- Loading multiple images within a folder
- Supporting extension filtering
- Thumbnail display
- Cooperating with workspace document tabs for opening and releasing

## Regarding RBAC, What This Module Currently Actually Handles

The biggest problem with old documentation is writing `ColorVision.Solution` as the "project-wide unified RBAC permission control layer." The current code is not in this state.

### Current Real Situation

`Rbac/` is indeed an important sub-module of `ColorVision.Solution`, already containing:

- `RbacManager`
- `LoginWindow`, `UserManagerWindow`, `PermissionManagerWindow`
- Users, roles, permissions, sessions, audit-related entities and services
- Local SQLite persistence
- Fine-grained permission code cache in `PermissionChecker`

### But Current Boundaries Must Also Be Clear

This RBAC system currently primarily acts within its own management windows and the Solution-side local permission subsystem.

From current search results, fine-grained calls like `HasPermissionAsync` and `PermissionChecker` are almost all still within the `Rbac/` subdirectory; meanwhile, many window entry points still first rely on the global `Authorization.Instance.PermissionMode` for coarse-grained judgments.

So a more accurate description is:

- `ColorVision.Solution` contains a local RBAC sub-module
- It coexists with the global `PermissionMode`
- The entire solution tree, all editors, and all file operations cannot be described as having been fully integrated with fine-grained permission code control

## Using It as a DLL

### When to Reference It

- `.cvsln` workspace, file tree, recent files, or workspace status-bar support is needed.
- Pluggable file editors, folder editors, or generic editors are needed.
- AvalonDock document area, layout save/restore, or panel Providers are needed.
- Built-in terminal, Markdown preview, multi-image preview, or local RBAC management windows are needed.

### Adding a File Editor

1. Implement `IEditor`, usually by inheriting `EditorBase`.
2. Add `EditorForExtensionAttribute`, `GenericEditorAttribute`, or `FolderEditorAttribute`.
3. Confirm `EditorManager` can scan the type.
4. Open the target file or folder and verify editor selection, default editor, and document activation.

### Adding a Project or File Template

1. New project templates implement `IProjectTemplate` and add `ProjectTemplateAttribute`.
2. New file templates implement `INewItemTemplate` and add `NewItemTemplateAttribute`.
3. Verify visibility through `AddNewProjectWindow` or `AddNewItemWindow`.

### Release Notes

`Solution` depends on `ImageEditor`, `UI.Desktop`, AvalonDock, AvalonEdit, WebView2, and WPFHexaEditor. After publishing, verify `.cvsln` open, file tree, text editor, image editor, terminal, and layout recovery.

### DLL Release Acceptance

| Check | What to Inspect | Passing Standard |
| --- | --- | --- |
| Target framework output | `net10.0-windows7.0` | DLL, `.nupkg`, and `.snupkg` are produced |
| Package README | `PackageReadmeFile`, package root | `README.md` is included at the package root |
| Project dependencies | `ColorVision.Database`, `ColorVision.ImageEditor`, `ColorVision.UI.Desktop`, `ColorVision.UI` | Workspace, database, image editor, and desktop toolkit dependencies resolve |
| Third-party dependencies | `AvalonEdit`, `AvalonDock`, `WebView2`, `WPFHexaEditor`, `Markdig.Signed` | Text editing, docking layout, Markdown/Web, and Hex viewing load |
| Solution entry | `SolutionManager`, `OpenSolutionWindow` | `.cvsln`, folder, and recent-file open paths work |
| Editor registration | `EditorManager`, `EditorForExtensionAttribute`, `FolderEditorAttribute` | Text, image, Web, Hex, and folder editors can be scanned and selected |
| Workspace layout | `WorkspaceManager`, `DockLayoutManager` | Tabs and panel layout save, load, and reset correctly |
| Terminal and disposal | `TerminalControl`, `TerminalService` | Opening/closing terminal does not leave shell processes behind; timers/processes release on exit |
| Local RBAC | `RbacManager`, `RbacManagerConfig` | Login/logout and user/role/permission management windows open, with clear session boundaries |

### Field First Checks

| Symptom | First Check |
| --- | --- |
| `.cvsln` does not open or recent file fails | Check `SolutionManager` path normalization, file existence, and directory permissions |
| Double-clicked file has no suitable editor | Check whether `EditorManager` scanned the related Attribute and whether default-editor config points to an old type |
| Tabs disappear after layout restore | Check `DockLayoutManager` layout file and whether `ContentId` is stable |
| Terminal opens blank or cannot type | Check ConPTY initialization, shell path, current solution directory, and `TerminalControl.Dispose()` |
| Markdown/WebView2 is blank | Check WebView2 Runtime, user-data-folder permission, and `WebEditor` initialization |
| Multi-image viewer is slow or thumbnails fail | Check image count, extension filtering, thumbnail cache, and disposal flow |
| RBAC login state is abnormal | Distinguish Solution local RBAC from global `PermissionMode`; start with `RbacManagerConfig` and local SQLite |

## How to Better Read This Project Currently

### To View Solution Entry Points

Read first:

- `SolutionManager.cs`
- `SolutionManagerInitializer.cs`
- `OpenSolutionWindow.xaml(.cs)`

### To View Tree and File Nodes

Read first:

- `Explorer/SolutionExplorer.cs`
- `Explorer/SolutionNodeFactory.cs`
- `TreeViewControl.xaml(.cs)`

### To View How Files Are Opened by Different Editors

Read first:

- `Editor/EditorManager.cs`
- `Editor/EditorForExtensionAttribute.cs`
- `Editor/*.cs`

### To View Workspace Layout and Document Hosting

Read first:

- `Workspace/WorkspaceManager.cs`
- `Workspace/DockLayoutManager.cs`
- `Workspace/LayoutMenuItems.cs`

### To View Local Permission Subsystem

Read first:

- `Rbac/RbacManager.cs`
- `Rbac/Services/`
- `Rbac/Entity/`

## What This Page No Longer Does

This page no longer continues to maintain these high-risk contents:

- Outdated version numbers and target framework lists
- Extensive pseudocode assuming the existence of a complete public API
- Writing `RbacManager` as the project-wide unified permission entry point
- Writing all file operations as having been completely intercepted by fine-grained permissions

If specific classes or methods need to be supplemented, they should be expanded separately on the corresponding sub-module pages rather than continuing to stack an entire page of pseudo-APIs here.

## Continue Reading

- [UI Components Overview](./README.md)
- [Security and Permission Control](../../03-architecture/security/overview.md)
- [RBAC Module](../../03-architecture/security/rbac.md)
