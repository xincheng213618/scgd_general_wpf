# ProjectARVRPro.IntegrationDemo

`Projects/ProjectARVRPro.IntegrationDemo/` is a minimal TCP/JSON integration sample for customers, MES, PLC, or automation controllers. It is not a ColorVision plugin and should not depend on ColorVision internal algorithm DLLs.

## Project Positioning

| Item | Value |
| --- | --- |
| Target framework | .NET Framework 4.8 |
| Shape | WPF demo window plus CLI arguments |
| ColorVision dependency | None |
| Purpose | Demonstrate ARVRPro TCP connection, commands, result parsing, and CSV export |

## Capabilities

- Connect to the ARVRPro TCP port, usually `6666`.
- Send `ProjectARVRInit`, `SwitchPGCompleted`, `RunAll`, and `AOITestSwitchImageComplete`.
- Load sample JSON or a field-saved `ProjectARVRResult` JSON file.
- Display `ObjectiveTestResult` and a flat item table.
- Save raw JSON and export CSV.
- Demonstrate JSON object reading across partial or sticky TCP packets.

## Public Contract Boundary

Customer-reusable contract code lives under:

- `Contracts/ObjectiveTestResult.cs`
- `Contracts/ObjectiveTestItem.cs`
- `Contracts/Process/`
- `Contracts/Socket/`
- `Contracts/MVVM/ViewModelBase.cs`

These files describe JSON fields only. They must not import ARVRPro process, algorithm, database, or host UI logic.

## Integration Event Sequence

| Stage | Demo behavior | ARVRPro expectation | Handoff focus |
| --- | --- | --- | --- |
| Connect | `TcpClient` connects to `host:port` | ARVRPro Socket service is listening, usually on `6666` | Confirm port, firewall, and that ProjectARVRPro is loaded |
| Initialize | Sends `ProjectARVRInit` with `SerialNumber` | Host creates the current SN and process context | SN must match the customer MES/controller trace key |
| Full run | Sends `RunAll` | Host executes the current process group | Use only after process group, recipe, and picture switching are configured |
| Normal picture confirmation | Sends `SwitchPGCompleted` after receiving `SwitchPG` | Host continues to the next process node | Demo uses `MsgID`, SN, and `ARVRTestType` to avoid duplicate confirmation |
| AOI picture confirmation | Sends `AOITestSwitchImageComplete` after receiving `AoiSwitchPG` | Host continues the AOI Relay chain | Do not send this if the site does not use AOI Relay |
| Result parsing | Parses and saves JSON/CSV after `ProjectARVRResult` | Host returns `ObjectiveTestResult` | Public field changes must update `Contracts/`, samples, and CSV docs |

`JsonStreamMessageReader` is the key handoff point in this demo. Customer controllers should not read TCP JSON with plain `ReadLine()` or fixed-length buffers; partial and sticky packets are expected in the field.

## Common Commands

Run the demo window:

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo
```

Parse sample JSON offline:

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --parse-file Projects/ProjectARVRPro.IntegrationDemo/Samples/project-arvr-result.json
```

Initialize online:

```powershell
dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --sn SN001 --mode init
```

Publish for customers:

```powershell
dotnet publish Projects/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo.csproj -f net48 -c Release -p:Platform=x64 -o artifacts/ProjectARVRPro.IntegrationDemo
```

## Handoff Notes

- Keep this as a customer-side sample, not an internal ColorVision business project.
- When public fields change, update `Contracts/`, sample JSON, README, and this page together.
- Customer TCP readers must handle partial and sticky packets; the demo reader is the reference.

## Handoff Acceptance

| Check | Action | Pass criteria |
| --- | --- | --- |
| Offline parsing | Run `--parse-file` with sample or field JSON | Raw JSON copy and flat CSV are generated; EventName, SN, Code, Msg, and TotalResult are readable |
| Online init | Run `--mode init --sn <SN>` while ARVRPro is running | Demo receives `SwitchPG` or a final result without JSON reader errors |
| Online full run | Run `--mode runall` | Flow reaches `ProjectARVRResult`; exported CSV keeps all expected fields |
| Confirmation de-duplication | Let the host repeat the same picture event | Demo confirms once and logs duplicate skip behavior |
| CSV fields | Open exported CSV | Includes Screen, Item, Description, Value, TestValue, Unit, LowLimit, UpLimit, TestResult, and Path |
| Customer package | Run `dotnet publish ... -f net48` | Output can run without ColorVision host DLLs |

## First Checks

| Symptom | First check | Interpretation |
| --- | --- | --- |
| Cannot connect | ARVRPro Socket service, port `6666`, firewall | Demo is only the client and will not start host services |
| JSON arrives but flow stops | `EventName`, duplicate `MsgID`, real picture switch completion | Confirm the controller actually switched the image |
| Parsed result is empty | Whether `Data` still matches `ObjectiveTestResult` | Host fields probably changed without contract sync |
| CSV misses fields | `ResultParser.WriteCsv` and `ResultItem` | New public fields need flattening rules |
| Customer asks for algorithm changes | Not this project | The demo owns protocol and parsing only; algorithms stay in ProjectARVRPro |
