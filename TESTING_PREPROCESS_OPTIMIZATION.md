# PreProcessManager Optimization - Manual Testing Guide

## Testing Overview
This document provides manual testing steps for the PreProcessManager optimization changes.

## What Changed

### Backend Changes
1. **PreProcessMeta**: Added `IsEnabled` property (defaults to `false`)
2. **PreProcessManager**: 
   - Removed manual Add/Remove/Update functionality
   - Auto-populates all template+preprocessor combinations in `InitializeAllPreProcessors()`
   - Persistence now includes `IsEnabled` state
3. **DisplayFlow**: Filters preprocessors by `IsEnabled` flag during execution

### UI Changes
1. **PreProcessManagerWindow**: 
   - Removed "Create" and "Update" sections
   - Added "Enable" checkbox column in list view
   - Added IsEnabled checkbox in property panel
   - Simplified to show all preprocessors with enable toggles

## Manual Testing Steps

### Test 1: Initial Auto-Population
**Objective**: Verify all preprocessors are auto-created for all templates

**Steps**:
1. Delete existing config file: `%AppData%\ColorVision\Config\PreProcessConfig.json`
2. Launch ColorVision application
3. Open PreProcessManager window (from Flow menu)
4. Verify that list shows entries for all template+preprocessor combinations
5. Verify all entries have IsEnabled = unchecked by default

**Expected Result**: 
- All possible template+preprocessor combinations are present
- All entries disabled by default

### Test 2: Enable/Disable Toggle
**Objective**: Verify enable/disable checkbox functionality

**Steps**:
1. Open PreProcessManager window
2. Check the "Enable" checkbox for one preprocessor entry
3. Close and reopen PreProcessManager window
4. Verify the enabled state is persisted

**Expected Result**:
- Checkbox state changes immediately
- State persists across application restarts

### Test 3: Flow Execution with Enabled Preprocessor
**Objective**: Verify enabled preprocessors execute during flow

**Steps**:
1. Enable "FolderSizePreProcess" for a specific flow template
2. Configure the FolderSizePreProcess (edit folder path, size limit, etc.)
3. Run the flow
4. Check logs for preprocessor execution messages

**Expected Result**:
- Enabled preprocessor executes before flow starts
- Log shows: "匹配到 1 个已启用的预处理 [FlowName]"
- Log shows: "执行预处理 [MetaName] -> FolderSizePreProcess"

### Test 4: Flow Execution with Disabled Preprocessor
**Objective**: Verify disabled preprocessors are skipped

**Steps**:
1. Disable all preprocessors for a flow template
2. Run the flow
3. Check logs

**Expected Result**:
- No preprocessor execution messages in logs
- Flow runs normally without preprocessing

### Test 5: Configuration Persistence
**Objective**: Verify preprocessor configuration is saved correctly

**Steps**:
1. Select a preprocessor entry
2. Enable it via checkbox
3. Click edit config button (gear icon)
4. Modify configuration values
5. Close configuration window
6. Close PreProcessManager window
7. Restart application
8. Open PreProcessManager and select same entry

**Expected Result**:
- IsEnabled state persists
- Configuration values persist
- Processor configuration retained correctly

### Test 6: Multiple Preprocessors for Same Template
**Objective**: Verify execution order with multiple enabled preprocessors

**Steps**:
1. Enable 2+ preprocessors for the same flow template
2. Use Move Up/Down buttons to order them
3. Run the flow
4. Check logs for execution order

**Expected Result**:
- Preprocessors execute in list order (top to bottom)
- Execution order displayed correctly in UI (序号 column)
- All enabled preprocessors execute sequentially

### Test 7: UI Simplification Verification
**Objective**: Verify UI no longer has create/update/delete functionality

**Steps**:
1. Open PreProcessManager window
2. Verify UI layout

**Expected Result**:
- No "Create" section with name/template/process dropdowns
- No "Update" section
- No "Delete" button
- Only "Move Up" and "Move Down" buttons present
- Enable checkbox visible in list and property panel

## Known Issues / Notes

1. **Migration**: Existing users will see their previous preprocessor entries plus auto-generated ones. This is expected - they can disable unwanted entries.

2. **Performance**: Auto-population happens on startup. With many templates and preprocessors, this may take a moment on first run.

3. **Default State**: All auto-generated entries default to `IsEnabled = false` to avoid surprising users with unexpected preprocessing.

## Verification Checklist

- [ ] All template+preprocessor combinations auto-populate on first run
- [ ] Enable/disable checkbox works in list view
- [ ] Enable/disable checkbox works in property panel
- [ ] Enabled state persists across restarts
- [ ] Preprocessor configuration persists correctly
- [ ] Only enabled preprocessors execute during flow
- [ ] Multiple enabled preprocessors execute in correct order
- [ ] UI no longer has Add/Remove/Update functionality
- [ ] Move Up/Down still works for ordering
- [ ] Configuration editing still works
- [ ] Log messages correctly show preprocessor execution

## Code Quality Verification

- [x] Project builds without errors (warnings only)
- [x] No breaking changes to public API
- [x] Persistence format updated to include IsEnabled
- [x] DisplayFlow correctly filters by IsEnabled flag
- [x] UI simplified and more user-friendly
