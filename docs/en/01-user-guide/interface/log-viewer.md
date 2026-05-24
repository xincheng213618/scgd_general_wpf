# Log Viewer

The log viewer is used for locating startup anomalies, device communication issues, workflow execution errors, and plugin loading failures. When encountering problems like "interface unresponsive, feature not taking effect, device not connecting," checking logs first is usually faster than guessing.

## When to Open It

- Main window or a feature fails to open
- Device connection status abnormal
- Workflow execution error but interface prompts are insufficiently clear
- Plugin, service, or template loading behavior abnormal

## Features You Will Typically Use

- View new logs in real-time
- Search by keywords
- Filter by log level
- View a certain range of historical logs
- Switch between auto-scroll and manual reading

## Basic Usage

1. Open the log window from the Help menu or corresponding entry point.
2. First view recent logs to confirm the approximate time point of the error.
3. Use keywords to search for module names, device names, exception keywords, or file names.
4. If there are too many logs, raise the level to `Warn` or `Error` to narrow the scope.

## How to Search More Efficiently

- First use simple keywords, such as device name, template name, `error`, `timeout`
- When results are too many, combine multiple keywords
- Only use regular expressions when absolutely necessary

If the search box turns red after input, it usually means the regex is wrong; at this point, falling back to ordinary keyword search is more stable.

## Common Issues

### Almost No Content After Opening Log Window

- First check whether auto-refresh is enabled
- Then check whether the current log level is too high
- If you only want to see new logs, confirm the current loading policy is not empty

### Too Many Logs, Interface Hard to Read

- First switch level to `Warn` or `Error`
- Then use keywords to narrow the scope
- If it's just temporary troubleshooting, don't try to preserve all historical logs first

### Search Results Incorrect

- First confirm whether multiple keywords were all set as must-match-simultaneously
- If regex was used, first confirm whether special characters need escaping
- When search finds nothing, return to unfiltered view to find the time point again

## Troubleshooting Suggestions

### Device Issues

- First search for device name, camera code, service name
- Then cross-check with [Device Service Overview](../devices/overview.md)

### Workflow Issues

- First search for template name, node name, or execution failure keywords
- Then cross-check with [Workflow Execution & Debugging](../workflow/execution.md)

### Plugin or Startup Issues

- First view recent logs from the application startup phase
- Then pay attention to whether there are DLL, version, path, or loading failure prompts

## Continue Reading

- [Main Window Guide](./main-window.md)
- [Terminal](./terminal.md)
- [Common Issues](../troubleshooting/common-issues.md)

## Notes

- This page only retains the usage and troubleshooting entry points for the log viewer and no longer maintains underlying log implementation documentation.
- Related implementations are primarily located in `UI/ColorVision.UI/LogImp/`.