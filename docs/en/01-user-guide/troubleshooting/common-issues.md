# Common Issues

This page is not code analysis but a troubleshooting entry point. When encountering problems, first determine which category it belongs to, then jump to the corresponding page to further narrow the scope.

## Troubleshoot in This Order First

1. First check [Log Viewer](../interface/log-viewer.md) to confirm whether the error occurred during startup, device, workflow, or plugin phase.
2. Then confirm whether the current project, device instance, template, or window is the object you think it is.
3. Then check external conditions such as paths, licenses, database connections, and service status.
4. Finally, consider whether you need to reopen windows, re-save configurations, or restart related services.

## Common Scenarios

### Cannot Find Feature in Main Window

- First return to [Main Window Guide](../interface/main-window.md)
- Prioritize using the search box to locate the entry point
- If it's a plugin feature, confirm whether the plugin has been correctly loaded

### Device Exists but Cannot Work

- First check [Device Service Overview](../devices/overview.md)
- For camera scenarios, continue with [Camera Service](../devices/camera.md)
- If it's a binding or hardware access issue, then check [Physical Camera Management](../devices/camera-management.md)

### Parameters Changed but Effect Unchanged

- First confirm that you modified the object currently in use
- Then confirm whether it has been saved and related windows or services have been reloaded
- For property and template related scenarios, prioritize returning to [Property Editor](../interface/property-editor.md) or the corresponding device parameter page for recheck

### Workflow Execution Failed

- First check [Workflow Execution & Debugging](../workflow/execution.md)
- In logs, prioritize searching for node name, template name, and failure keywords
- Do not immediately suspect the entire workflow; first narrow down to the first failed node

### JSON or Template Content Validation Failed

- First use the validation button in the editor to confirm whether it's a format issue
- Then check whether comment text, description text, or old template content was mistakenly pasted into JSON
- If you switched between different editing modes, first return to the currently saved JSON before judging

### Too Many Logs, Cannot See Key Points

- First switch log level to `Warn` or `Error`
- Then use keywords to narrow the scope
- If the search box turns red, first abandon regex and fall back to ordinary keywords

## If Still Not Located

- Record the time point when the problem occurred
- Note the current project, device name, template name, or plugin name
- Attach corresponding log snippets before continuing troubleshooting; efficiency will be significantly higher

## Continue Reading

- [Main Window Guide](../interface/main-window.md)
- [Log Viewer](../interface/log-viewer.md)
- [Device Service Overview](../devices/overview.md)
- [Workflow](../workflow/README.md)

## Notes

- This page only retains troubleshooting paths and no longer maintains generalized project structure analysis manuscripts.