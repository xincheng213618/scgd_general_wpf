# ColorVision.SocketProtocol

This page only describes the communication implementation currently implemented in UI/ColorVision.SocketProtocol, no longer continuing the old documentation's "generic JSON protocol layer examples" and mismatched message model descriptions.

## Module Positioning

ColorVision.SocketProtocol is currently a desktop-side local TCP communication module, primarily responsible for:

- Starting and stopping the Socket server
- Dispatching JSON or plain text requests
- Persisting message records to SQLite
- Providing management window and status bar entry points
- Integrating with the settings system

It is not an abstract "device protocol specification document," but an actual module already coupled with UI, configuration, and database browsing entry points.

## Most Critical Files

From the project directory, the most worthwhile to read first are:

- `SocketManager.cs`: Server, client, dispatcher, and message management main entry point
- `SocketInitializer.cs`: Startup initialization and start/stop listener
- `SocketConfig.cs`: Communication configuration
- `ISocketJsonHandler.cs`: JSON request handling extension point
- `SocketMessage.cs`: Message persistence entity
- `SocketMessageManager.cs`: SQLite persistence and queries
- `SocketManagerWindow.xaml(.cs)`: Management and viewing window
- `SocketStatusBarProvider.cs`: Status bar entry point
- `SocketConfigProvider.cs`: Settings system integration point

## Key Entry Point Types

### SocketManager

`SocketManager` is the central object of the current communication module. It is responsible for:

- Holding `SocketConfig`
- Creating `SocketJsonDispatcher` and `SocketTextDispatcher`
- Managing `SocketMessageManager`
- Starting and stopping the server
- Tracking connection status
- Exposing configuration editing commands

If reading only one file to understand the entire module, the first choice is `SocketManager.cs`.

### SocketInitializer

The current module does have `SocketInitializer`, and it is one of the actual startup entry points. It will:

- Read `SocketConfig.Instance.IsServerEnabled` at startup
- Call `SocketManager.GetInstance().StartServer()` when enabled
- Subscribe to `ServerEnabledChanged` to dynamically start/stop service at runtime

This means whether the communication service goes online is currently primarily configuration-driven, not solely relying on the user manually opening a window.

### SocketConfig

`SocketConfig` currently mainly includes:

- Whether to enable the server
- Listening IP
- Port
- Buffer size
- Protocol mode: `Json` or `Text`

Fields like timeout and auto-reconnect written in old documentation are not real configuration items in the current class.

### SocketJsonDispatcher / SocketTextDispatcher

Current protocol dispatch is split into two sets:

- `SocketJsonDispatcher`: Scans `ISocketJsonHandler`
- `SocketTextDispatcher`: Scans `ISocketTextDispatcher`

Among them, JSON handlers currently match by `EventName`. The real request and response models are:

- `SocketRequest`: `Version`, `MsgID`, `EventName`, `SerialNumber`, `Params`
- `SocketResponse`: `Version`, `MsgID`, `EventName`, `SerialNumber`, `Code`, `Msg`, `Data`

Therefore, it is not the generalized `type/data/timestamp` message format of old documentation.

### SocketMessage / SocketMessageManager

Current message persistence is not a concept-layer feature, but directly lands in SQLite. `SocketMessage` primarily stores:

- Client address
- Direction (receive/send)
- Content
- Time
- EventName / MsgID / ResponseCode

`SocketMessageManager` is responsible for:

- Initializing `SocketMessages.db`
- Loading recent messages
- Inserting, deleting, and querying messages
- Opening the database file location
- Providing database browsing entry points

The database default path is at:

- `%AppData%/ColorVision/Config/SocketMessages.db`

### SocketManagerWindow and SocketStatusBarProvider

Currently, the main user-side entry points are not a pile of protocol sample code, but two UI integration points:

- `SocketManagerWindow`: View history messages, message details, copy, resend, delete
- `SocketStatusBarProvider`: Reflect connection status in the status bar, click to open the management window

Additionally, a menu entry class `MenuProjectManager` is defined in `SocketManagerWindow.xaml.cs`, currently placed under the Help menu to open the management window.

The current management window is no longer just the minimal form of "message list + details." The top of the window displays service enabled status, whether the service is open, listening address, protocol mode, and client count; when opening fails, it directly shows the last error message. The message area supports text filtering, direction filtering, auto-scrolling, and list virtualization; the right side organizes information through "Message Details / Connected Clients / Service Diagnostics" tabs, with the details area supporting JSON formatted viewing. When resending messages, it preferentially matches connections by original client address; if not found, the currently selected client can be used as a fallback target.

Common keyboard shortcuts:

- `Ctrl+F`: Focus filter box
- `Esc`: Clear filter
- `F5`: Reload recent messages
- `Ctrl+C`: Copy selected message content
- `Delete`: Delete selected message

## Current Runtime Main Chain

The existing chain is roughly:

1. `SocketInitializer` starts and listens to `SocketConfig.Instance.IsServerEnabled`.
2. When the service is enabled, `SocketManager` starts the TCP server.
3. After receiving a request, dispatch via JSON or Text based on the currently configured protocol mode.
4. JSON requests match to `ISocketJsonHandler` implementations by `EventName`.
5. Sent and received messages are written to the SQLite database managed by `SocketMessageManager`.
6. `SocketStatusBarProvider` and `SocketManagerWindow` read status and message lists from the manager.

## Using It as a DLL

### When to Reference It

- A project package needs to expose a local TCP interface for customer equipment, host software, or test tools.
- JSON requests must dispatch to business handlers by `EventName`.
- Sent and received messages need to be stored in SQLite for on-site tracing.
- The main UI needs a status-bar service indicator and a management window.

### Adding a JSON Handler

1. Add a class implementing `ISocketJsonHandler`.
2. Set a unique `EventName`.
3. Return `SocketResponse` or equivalent response data from the handler.
4. Confirm the assembly is loaded so the dispatcher can scan the handler.
5. Use `SocketManagerWindow` to inspect received requests, response codes, and message history.

### Release Notes

The Socket module depends on runtime configuration. After upgrading the DLL, keep or migrate `%AppData%/ColorVision/Config/SocketMessages.db` and Socket configuration. Otherwise the field symptom may look like service disabled, changed port, or missing history.

### DLL Release Acceptance

| Acceptance item | What to check | Pass condition |
| --- | --- | --- |
| Target frameworks | `ColorVision.SocketProtocol.csproj` targets `net8.0-windows7.0;net10.0-windows7.0` | The host can load the matching DLL. |
| Package metadata | `GeneratePackageOnBuild`, `PackageReadmeFile`, `README.md` | Package README exists and version is traceable. |
| Upper dependencies | `ColorVision.UI`, `ColorVision.Database`, `log4net`, `Newtonsoft.Json` | Runtime output contains all dependencies without load failures. |
| Service lifecycle | `SocketInitializer`, `SocketConfig.Instance.IsServerEnabled`, `SocketManager` | Enabling listens successfully; disabling releases the port. |
| Protocol dispatch | JSON / Text modes, `ISocketJsonHandler.EventName` | JSON events match handlers and Text mode is not accidentally removed. |
| Message persistence | `%AppData%/ColorVision/Config/SocketMessages.db`, `SocketMessageManager` | Sent and received messages are written and visible in the window. |
| UI integration | `SocketStatusBarProvider`, `SocketManagerWindow` | Status bar, management window, filters, and diagnostics are usable. |
| Configuration migration | Real `SocketConfig` fields | Port, enabled state, and protocol mode persist or have an explicit migration note. |

### Field First Checks

| Symptom | Check first | Judgement point |
| --- | --- | --- |
| Service is enabled but no port is listening | `SocketConfig.IsServerEnabled`, port conflict, latest `SocketManager` error | The diagnostics tab's last error is the first evidence. |
| JSON request does not enter business logic | `EventName`, whether the `ISocketJsonHandler` assembly is loaded | The dispatcher cannot scan handlers from unloaded assemblies. |
| External equipment receives malformed response | `SocketResponse`, JSON serialization, exception wrapping | Inspect raw request and response in the management window. |
| Message history is empty or lost | `SocketMessages.db` path and permissions | Upgrade packages must not overwrite or delete the field database. |
| Resend fails | Client list, original client address, current selected client | The current logic first matches original address, then falls back to selected client. |
| Config changes still use the old port | Config save, service restart, old listener release | Communication changes usually need the Socket service chain to restart. |

## What Boundaries the Current Implementation Has

### It Is Not a Pure JSON Protocol Library

Although JSON is one of the main modes, the current implementation simultaneously supports `SocketPhraseType.Text`. Writing the entire module as a "unified JSON protocol layer" would miss the text mode and real responsibilities of status bar, window, and persistence.

### It Is Not Just Handler Interfaces

Old documentation placed emphasis on `ISocketJsonHandler`, but the current module's value equally comes from:

- Initializer
- Management window
- Status bar entry point
- SQLite message history

If only writing handler extension points, it is easy to flatten the module.

### Configuration Fields Must Be Described Based on the Real Class

The current `SocketConfig` does not have the fields `ReceiveTimeout`, `SendTimeout`, `AutoReconnect` claimed in old documentation. When describing communication configuration, you must use the real properties as the standard.

## How to Better Read This Module Currently

### To View Server and Dispatch Main Chain

Read first:

- `SocketManager.cs`
- `SocketInitializer.cs`
- `ISocketJsonHandler.cs`

### To View Settings and Status Bar Integration

Read first:

- `SocketConfig.cs`
- `SocketConfigProvider.cs`
- `SocketStatusBarProvider.cs`

### To View Message History and Management Window

Read first:

- `SocketMessage.cs`
- `SocketMessageManager.cs`
- `SocketManagerWindow.xaml.cs`

## Optimization Roadmap

Subsequent optimization recommendations for this module advance in four layers:

| Phase | Goal | Focus |
| --- | --- | --- |
| P0 Stability | Tighten service lifecycle and TCP boundaries | Prevent duplicate starts, cancellation tokens, unified stop paths, sticky/unpacked packet handling |
| P1 Observability | Improve on-site troubleshooting efficiency | Message export, connection lifecycle, error statistics, processing duration |
| P2 Protocolization | Reduce external device integration cost | Error codes, Handler metadata, JSON Schema, version compatibility |
| P3 Performance & Capacity | Support long-running and larger history volumes | Paginated loading, database indexes, batch writes, retention policies |

For detailed roadmap, see [Socket Communication Module Optimization Roadmap](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md).

## What This Page No Longer Does

This page no longer continues to maintain these high-risk contents:

- Fabricated unified message field models
- Configuration item lists mismatched with the real class
- Introductions with only handler examples, no management window and persistence boundaries
- Writing the current module as a pure protocol specification rather than an actual UI communication module

## Continue Reading

- [UI Components Overview](./README.md)
- [ColorVision.Database](./ColorVision.Database.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [Socket Communication Module Optimization Roadmap](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md)
