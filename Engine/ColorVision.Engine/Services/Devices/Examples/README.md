# PropertyVisibilityAttribute Examples

This directory contains examples demonstrating how to use the `PropertyVisibilityAttribute` with enum support.

## Files

- **EnumVisibilityExample.cs**: Comprehensive example showing:
  - Enum-based visibility (new feature)
  - Boolean-based visibility (original feature)
  - Inverted visibility logic
  - Multiple enums controlling different properties
  - Mixing boolean and enum visibility in the same class

## How to Use

1. Create an instance of `EnumVisibilityExample`
2. Open it in a `PropertyEditorWindow`
3. Change the `Operation Mode` or `Connection Type` to see different properties appear/disappear

## Key Concepts Demonstrated

### Enum-Based Visibility
```csharp
[PropertyVisibility(nameof(Mode), OperationMode.Basic)]
public string BasicParam1 { get; set; }
```
This property is only visible when `Mode == OperationMode.Basic`.

### Inverted Enum Visibility
```csharp
[PropertyVisibility(nameof(Mode), OperationMode.Basic, isInverted: true)]
public int NonBasicParam { get; set; }
```
This property is hidden when `Mode == OperationMode.Basic` (visible for all other modes).

### Boolean-Based Visibility (Original)
```csharp
[PropertyVisibility(nameof(EnableDebugging))]
public int DebugLevel { get; set; }
```
This property is visible when `EnableDebugging == true`.

## See Also

- [ENUM_VISIBILITY_USAGE.md](../../../UI/ColorVision.UI/PropertyEditor/ENUM_VISIBILITY_USAGE.md) - Complete usage guide
- [PropertyVisibilityAttribute.cs](../../../UI/ColorVision.UI/PropertyEditor/PropertyEditorTypeAttribute.cs) - Attribute implementation
