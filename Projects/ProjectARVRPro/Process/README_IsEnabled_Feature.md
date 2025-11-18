# ProcessMeta IsEnabled Feature

## Overview
This feature adds an `IsEnabled` property to `ProcessMeta` to allow selective execution of process steps. When a ProcessMeta is disabled, it will be skipped during the test execution flow.

## Properties
- **IsEnabled**: Boolean property (default: `true`)
  - When `true`: ProcessMeta is included in execution
  - When `false`: ProcessMeta is skipped during execution

## Behavior

### Example Scenario
If you have 8 ProcessMetas (indices 0-7) and only ProcessMetas at index 0 and 7 are enabled:

```
ProcessMetas[0].IsEnabled = true   // Enabled
ProcessMetas[1].IsEnabled = false  // Disabled
ProcessMetas[2].IsEnabled = false  // Disabled
ProcessMetas[3].IsEnabled = false  // Disabled
ProcessMetas[4].IsEnabled = false  // Disabled
ProcessMetas[5].IsEnabled = false  // Disabled
ProcessMetas[6].IsEnabled = false  // Disabled
ProcessMetas[7].IsEnabled = true   // Enabled
```

### Execution Flow
1. **Initialization** (`FlowInit` in SocketControl):
   - System finds the first enabled ProcessMeta (index 0)
   - Sends `ARVRTestType = 0` to switch PG

2. **After completing step 0** (`SwitchPGCompleted` in ARVRWindow):
   - System searches for next enabled ProcessMeta after index 0
   - Finds ProcessMeta at index 7 (skipping 1-6)
   - Executes ProcessMeta at index 7

3. **After completing step 7** (`IsTestTypeCompleted` in ARVRWindow):
   - System checks if there are any enabled ProcessMetas after index 7
   - No more enabled ProcessMetas found
   - Returns `true`, indicating test completion

4. **Test Completion** (`TestCompleted` in ARVRWindow):
   - Sends final test results

## Implementation Details

### Modified Methods

#### ARVRWindow.cs
1. **SwitchPGCompleted()**
   - Searches for next enabled ProcessMeta starting from `CurrentTestType + 1`
   - Only processes enabled steps

2. **IsTestTypeCompleted()**
   - Checks if any enabled ProcessMetas exist after current step
   - Returns `true` if no more enabled steps

3. **SwitchPG()**
   - Sends the index of the next enabled ProcessMeta
   - If no enabled ProcessMeta found, sends `-1`

#### SocketControl.cs
1. **FlowInit.Handle()**
   - Finds first enabled ProcessMeta on initialization
   - No longer hardcoded to 0

### UI Changes
- ProcessManagerWindow now displays an "是否启用" (IsEnabled) column
- Users can toggle the checkbox to enable/disable each ProcessMeta
- Changes are automatically persisted to `ProcessMetas.json`

## Persistence
The `IsEnabled` state is saved to and loaded from `ProcessMetas.json` via:
- `ProcessManager.SavePersistedMetas()`
- `ProcessManager.LoadPersistedMetas()`

## Notes
- Default value is `true` to maintain backward compatibility
- Existing ProcessMetas without `IsEnabled` in JSON will default to enabled
- The UI checkbox updates are persisted immediately
