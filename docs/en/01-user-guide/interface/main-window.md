# Main Window Guide

The main window is the workbench of ColorVision. Most daily operations go through here, including devices, workflows, image viewing, logs, search, and status monitoring.

## What to Look at First After Entering the Main Window

### Top Menu and Search Area

- The menu is the primary entry point for functions
- The search box is used to quickly locate commands, pages, or objects
- When you don't know where a feature is hidden, prioritize using search first

### Left Navigation Area

- Commonly used to view projects, resource trees, or related view tabs
- If the current page has a lot of content, starting from the left structure is usually faster

### Center Main Work Area

- Displays the core content currently being operated on
- May be an image view, workflow window, template window, or other business interface
- The layout may switch between single view or multi-view depending on the current work mode

### Bottom Status Bar

- Used to observe system status, prompts, and some quick entry points
- When device, service, or plugin status is abnormal, first note whether there are prompts here

## What You Typically Do in the Main Window

- Open device-related windows
- Enter workflow design and execution pages
- View image or result windows
- Open logs, terminal, and configuration interfaces
- Search for feature entry points or object locations

## Suggested Familiarization Order

1. First get to know the main menu, search area, and status bar.
2. Then understand how left navigation and the main work area work together.
3. Next enter [Property Editor](./property-editor.md) and [Image Editor Overview](../image-editor/overview.md).
4. When hardware is involved, continue with [Device Service Overview](../devices/overview.md).
5. When automation is needed, enter [Workflow](../workflow/README.md).

## Common Issues

### Cannot Find a Feature Entry Point

- First use the search box to search keywords
- Then return to the corresponding chapter home page and re-enter
- If it's a plugin feature, check whether the plugin has been loaded

### Too Many Windows Open, Not Sure What You Are Currently Viewing

- Return to left navigation to reconfirm the current object
- Observe the main work area title and status bar information
- Re-enter the corresponding page by business, rather than blindly switching between multiple windows

### Expected Prompt Not Appearing in Status Bar

- First confirm whether the related service, device, or plugin has actually started
- Then check [Log Viewer](./log-viewer.md) for more specific information

## Continue Reading

- [UI Component User Handbook](./ui-component-handbook.md)
- [Property Editor](./property-editor.md)
- [Log Viewer](./log-viewer.md)
- [Terminal](./terminal.md)
- [Image Editor Overview](../image-editor/overview.md)

## Notes

- This page only retains the main window usage perspective and no longer maintains source code structure breakdown.
- Related implementations are primarily located in `ColorVision/MainWindow.xaml` and `ColorVision/MainWindow.xaml.cs`.
