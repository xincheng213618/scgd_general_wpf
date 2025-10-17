# ProcessMeta Update/Switch Feature

## Overview
Added the ability to update existing ProcessMeta entries in the ProcessManagerWindow without needing to delete and re-add them.

## Changes Made

### UI Changes (ProcessManagerWindow.xaml)
- Added new GroupBox titled "更新 ProcessMeta" (Update ProcessMeta)
- Contains two ComboBoxes:
  - Flow Template selector (流程模板)
  - Process Class selector (处理类)
- Added "更新" (Update) button to apply changes

### Logic Changes (ProcessManagerWindow.xaml.cs)
- Added `UpdateTemplate` property to hold the selected template for update
- Added `UpdateProcess` property to hold the selected process for update
- Added `UpdateMetaCommand` to handle the update action
- Implemented `OnSelectedProcessMetaChanged()` method:
  - Automatically populates update fields when a ProcessMeta is selected
  - Shows current FlowTemplate and Process values
- Implemented `CanUpdateMeta()` validation:
  - Ensures a ProcessMeta is selected
  - Ensures UpdateTemplate and UpdateProcess are set
- Implemented `UpdateMeta()` method:
  - Updates the selected ProcessMeta's FlowTemplate and Process
  - Changes are automatically persisted via existing mechanism

## How to Use

1. **Open ProcessManagerWindow** 
   - Launch the application
   - Navigate to the ProcessManager window

2. **Select a ProcessMeta**
   - Click on any ProcessMeta in the list view
   - The "Update ProcessMeta" section will automatically populate with current values

3. **Modify Values**
   - Change the "流程模板" (Flow Template) if needed
   - Change the "处理类" (Process Class) if needed

4. **Apply Update**
   - Click "更新" (Update) button
   - The selected ProcessMeta will be updated immediately
   - Changes are automatically saved to `ProcessMetas.json`

## Benefits

- **No need to delete and re-add**: Users can directly modify existing ProcessMeta entries
- **Preserves name**: The ProcessMeta name remains unchanged, only template and process are updated
- **Auto-population**: Current values are shown automatically when selecting an item
- **Validation**: Update button is disabled when selection is incomplete
- **Persistence**: Changes are automatically saved using the existing persistence mechanism

## Testing

### Manual Test Steps:
1. Create a ProcessMeta with template "Template1" and process "ProcessA"
2. Select it from the list
3. Verify the update section shows "Template1" and "ProcessA"
4. Change template to "Template2"
5. Click "更新" button
6. Verify the list shows the updated template
7. Close and reopen the window
8. Verify the changes persisted

### Expected Behavior:
- Update section should be populated when selecting a ProcessMeta
- Update button should be enabled only when valid selections are made
- Changes should be reflected immediately in the list
- Changes should persist after closing the window
