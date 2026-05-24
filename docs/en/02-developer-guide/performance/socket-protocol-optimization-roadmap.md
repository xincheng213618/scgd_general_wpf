# Socket Communication Module Optimization Roadmap

This document is aimed at `UI/ColorVision.SocketProtocol`, with the goal of advancing the currently available Socket management capabilities into a more stable, more easily troubleshootable communication module suitable for long-term operation.

## Current Structure Assessment

`ColorVision.SocketProtocol` has already formed a complete closed loop:

- `SocketInitializer` starts or stops services based on configuration
- `SocketManager` handles TCP listening, client management, and request dispatch
- `SocketJsonDispatcher` / `SocketTextDispatcher` handles JSON and text modes
- `SocketMessageManager` writes messages to SQLite
- `SocketManagerWindow` and `SocketStatusBarProvider` provide desktop-side O&M entry points

This means it is no longer "protocol example code" but a communication subsystem with UI, persistence, and runtime state. Optimization should prioritize protecting existing behavior, then gradually complete protocol boundaries and long-term operation capabilities.

## Completed UX Optimizations

The management window has been supplemented with a batch of low-risk capabilities:

- Top service status: enabled state, listening address, protocol mode, connection state, client count
- Open failure notification: last startup error displayed at top of window and on service diagnostics page
- Message filtering: filter by client, event name, MsgID, response code, content
- Direction filtering: all, received, sent
- List virtualization: reduces UI pressure when browsing historical messages
- Detail formatting: JSON content can be formatted for viewing, raw text copy also preserved
- Auto-scroll: can auto-position on new messages, or disabled
- Resend fallback: preferentially matches original client, falls back to currently selected client if not found
- Layout convergence: right side uses "Message Detail / Client / Service Diagnostics" tabs for auxiliary info
- Shortcuts: `Ctrl+F`, `Esc`, `F5`, `Ctrl+C`, `Delete`

## P0 Stability Roadmap

Goal: First tighten service lifecycle and TCP send/receive boundaries to reduce issues like "occasional hangs, port contention, message truncation" in the field.

| Item | Current Risk | Suggested Approach | Acceptance Criteria |
| --- | --- | --- | --- |
| Duplicate service start | Multiple calls to `StartServer()` create new background tasks | Add `_serverTask`, `CancellationTokenSource`, and start state protection | Consecutive start/stop clicks do not produce multiple listen loops |
| Service stop | `AcceptTcpClient()` relies on `TcpListener.Stop()` throwing exception to exit | Explicit cancel, catch cancel path, distinguish normal stop from error stop | Stop service logs no longer appear as exception errors |
| TCP boundaries | Currently treats one `Read` as one message | Introduce length prefix, delimiter, or configurable frame parser | Large messages and consecutive small messages do not stick/split |
| Client collection | `ObservableCollection<TcpClient>` interacting with background threads needs UI thread constraints | Encapsulate Add/Remove, all collection changes go through Dispatcher | Long-term connect/disconnect does not throw cross-thread exceptions |
| Client cleanup | Disconnect, stop, error paths are scattered | Add unified `CloseClient` method | After client disconnects, list, resources, and logs are consistent |

## P1 Observability Roadmap

Goal: Enable the window and logs to answer "who is connected now, what just happened, where failures are concentrated."

| Item | Suggested Approach | Acceptance Criteria |
| --- | --- | --- |
| Connection lifecycle | Add connection establishment, disconnection, error reason logging | Management window can see connection history or logs can locate |
| Message export | Support exporting current filter results as JSON/CSV | Can directly take problem samples from the field |
| Error statistics | Count JSON parse failures, Handler not found, send failures | Status bar or window can show failure counts |
| Processing latency | Record Handler execution time | Can locate slow events |
| Database retention policy | Clean up history by count or time | Long-term operation without unbounded growth |

## P2 Protocolization Roadmap

Goal: Advance external device integration from "convention" to "verifiable, compatible, migratable."

| Item | Suggested Approach | Acceptance Criteria |
| --- | --- | --- |
| Message model | Clarify mandatory fields for `Version`, `MsgID`, `EventName`, `SerialNumber`, `Params` | Documentation and runtime validation consistent |
| Error codes | Define unified error code ranges | Clients can make stable handling based on Code |
| Handler metadata | Add description, version, example parameters for handlers | Management window or documentation can enumerate supported events |
| Schema | Provide JSON Schema for key events | Integration parties can validate in advance |
| Compatibility strategy | Agree on new/old version field compatibility approach | Upgrades do not break existing devices |

## P3 Performance and Capacity Roadmap

Goal: Support longer runtime, higher message frequency, and larger message history.

| Item | Suggested Approach | Acceptance Criteria |
| --- | --- | --- |
| UI pagination | Management window loads history by pagination or incremental loading | Opening window does not lag due to large history database |
| Batch DB writes | Batch write or background queue write for high-frequency messages | UI not slowed by DB writes during message peaks |
| Indexes | Add indexes for `MessageTime`, `ClientEndPoint`, `EventName` | Query and filter historical data is controllable |
| Backpressure | Set queue limit and degradation strategy for extreme message rates | Can notify on overload rather than crashing the process |
| Stress testing | Add local TCP stress test scripts | Can reproduce capacity limits and regression results |

## Suggested Implementation Order

1. First do P0 service lifecycle protection and frame parsing to prevent protocol-layer risks from growing further.
2. Then add P1 export, statistics, and retention policies to improve on-site troubleshooting efficiency.
3. Then advance P2 protocol documentation, error codes, and Handler metadata.
4. Finally, based on actual message volume, decide on P3 pagination, batch DB writes, and stress testing investments.

## Related Documents

- [ColorVision.SocketProtocol API Guide](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md)
- [UI Component Overview](../../04-api-reference/ui-components/README.md)
- [Performance Optimization Guide](./overview.md)