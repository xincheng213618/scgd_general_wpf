# Pattern User Defaults Feature

## Overview
This feature adds an intermediate default configuration layer to the Pattern Window, allowing users to save their preferred configurations and reset to them quickly.

## Architecture

### Three Levels of Defaults
1. **Class Default (出厂默认)**: The original hardcoded default values defined in the pattern class
2. **User Default (用户默认)**: User-defined default values that can be saved and loaded
3. **Current Configuration (当前配置)**: The current working configuration

### Reset Behavior
- **PatternWindow Reset Button**: Resets to User Default if it exists, otherwise resets to Class Default
- **PropertyEditorWindow "恢复出厂" Button**: Always resets to Class Default, bypassing User Default

## User Interface Changes

### PatternWindow (Plugins/Pattern/PatternWindow.xaml)
Added two buttons in the pattern selection area:
- **"保存默认" (Save as Default)**: Saves the current configuration as the user default
- **"Reset"**: Resets to user default (if saved), otherwise to class default

### PropertyEditorWindow (UI/ColorVision.UI/PropertyEditor/PropertyEditorWindow.xaml)
Added one button in the bottom button bar:
- **"恢复出厂" (Reset to Factory)**: Resets directly to class default, ignoring user defaults

## Implementation Details

### PatternUserDefaultManager.cs
A new utility class that manages user default configurations:

```csharp
// Save current pattern configuration as user default
PatternUserDefaultManager.SaveUserDefault(pattern);

// Load user default configuration (returns null if not exists)
string userDefault = PatternUserDefaultManager.LoadUserDefault(patternType);

// Check if user default exists
bool exists = PatternUserDefaultManager.HasUserDefault(patternType);

// Delete user default
PatternUserDefaultManager.DeleteUserDefault(patternType);
```

### Storage Location
User defaults are stored in JSON files at:
```
Documents/ColorVision/Pattern/UserDefaults/{PatternTypeName}.json
```

Each pattern type has its own separate user default file, allowing per-pattern customization.

## Usage Workflow

### Scenario 1: First-time user
1. User opens PatternWindow and selects a pattern (e.g., "纯色")
2. Pattern loads with **Class Default** values
3. User modifies parameters (e.g., changes color, size mode, FOV)
4. User clicks **"保存默认"** to save their preferred configuration
5. Next time they select this pattern, it loads with their **User Default**
6. User can click **"Reset"** to quickly return to their **User Default**

### Scenario 2: Resetting to factory defaults
1. User has customized a pattern and saved it as User Default
2. User wants to see the original Class Default values
3. User clicks the settings icon (⚙️) to open PropertyEditorWindow
4. User clicks **"恢复出厂"** to reset to Class Default
5. The configuration reverts to original values, ignoring User Default

### Scenario 3: Removing user default
To remove a saved user default:
1. Delete the corresponding JSON file from: `Documents/ColorVision/Pattern/UserDefaults/`
2. Or save the Class Default as User Default by:
   - Opening PropertyEditorWindow
   - Clicking "恢复出厂"
   - Returning to PatternWindow
   - Clicking "保存默认"

## Benefits

1. **Faster Workflow**: Users can save their most-used configurations and reset to them quickly
2. **Flexibility**: Multiple patterns can each have their own user defaults
3. **Non-Destructive**: Original class defaults are always accessible via "恢复出厂"
4. **Per-Pattern**: Each pattern type (Solid, Checkerboard, Cross, etc.) has independent defaults
5. **Persistent**: User defaults are stored on disk and survive application restarts

## Technical Notes

### File Format
User defaults are stored as JSON files containing the serialized pattern configuration:
```json
{
  "MainBrush": "#FFFFFFFF",
  "Tag": "W",
  "BackGroundBrush": "#FF000000",
  "SizeMode": 0,
  "FieldOfViewX": 1.0,
  "FieldOfViewY": 1.0,
  "PixelWidth": 100,
  "PixelHeight": 100
}
```

### Pattern Type Identification
User defaults are keyed by the full type name (e.g., `Pattern.Solid.PatternSolid`), ensuring that different pattern implementations don't conflict.

### Error Handling
- If a user default file is corrupted, the system falls back to Class Default
- Save failures are reported to the user with error messages
- Load failures are logged but don't interrupt the application flow

## Testing Checklist

- [ ] Save user default for a pattern
- [ ] Reset to user default using PatternWindow Reset button
- [ ] Reset to class default using PropertyEditorWindow "恢复出厂" button
- [ ] Verify user defaults persist across application restarts
- [ ] Test with multiple pattern types
- [ ] Verify behavior when user default doesn't exist
- [ ] Test error handling with corrupted JSON files
- [ ] Verify different patterns have independent user defaults
