# PropertyVisibilityAttribute - Enum Support Usage Guide

## Overview

`PropertyVisibilityAttribute` has been extended to support enum-based visibility control. Properties can now be shown or hidden based on the value of an enum property, in addition to the existing boolean property support.

## Features

1. **Boolean-based visibility** (existing): Show/hide properties based on boolean values
2. **Enum-based visibility** (new): Show/hide properties based on specific enum values
3. **Inverted logic**: Both boolean and enum modes support inverted visibility

## Usage Examples

### 1. Basic Enum Visibility

Show a property only when an enum has a specific value:

```csharp
using System.ComponentModel;

public class MyConfig
{
    public enum OperationMode
    {
        Manual,
        Automatic,
        Advanced
    }

    [Category("General")]
    [DisplayName("Mode")]
    public OperationMode Mode { get; set; } = OperationMode.Manual;

    // This property is only visible when Mode == OperationMode.Manual
    [Category("Settings")]
    [DisplayName("Manual Setting")]
    [PropertyVisibility(nameof(Mode), OperationMode.Manual)]
    public string ManualSetting { get; set; } = "Manual Value";

    // This property is only visible when Mode == OperationMode.Automatic
    [Category("Settings")]
    [DisplayName("Automatic Setting")]
    [PropertyVisibility(nameof(Mode), OperationMode.Automatic)]
    public string AutomaticSetting { get; set; } = "Auto Value";
}
```

### 2. Inverted Enum Visibility

Hide a property when an enum has a specific value (show it for all other values):

```csharp
public class AdvancedConfig
{
    public enum SecurityLevel
    {
        Low,
        Medium,
        High
    }

    [Category("Security")]
    public SecurityLevel Level { get; set; } = SecurityLevel.Medium;

    // This property is HIDDEN when Level == SecurityLevel.Low
    // (visible for Medium and High)
    [Category("Security")]
    [DisplayName("Advanced Security Options")]
    [PropertyVisibility(nameof(Level), SecurityLevel.Low, isInverted: true)]
    public string AdvancedOptions { get; set; } = "Advanced";
}
```

### 3. Multiple Properties with Different Enum Values

Control multiple properties based on different enum values:

```csharp
public class CameraConfig
{
    public enum CameraMode
    {
        Photo,
        Video,
        Timelapse
    }

    [Category("General")]
    public CameraMode Mode { get; set; } = CameraMode.Photo;

    // Photo mode settings
    [Category("Photo Settings")]
    [PropertyVisibility(nameof(Mode), CameraMode.Photo)]
    public int PhotoQuality { get; set; } = 95;

    [Category("Photo Settings")]
    [PropertyVisibility(nameof(Mode), CameraMode.Photo)]
    public bool UseFlash { get; set; } = false;

    // Video mode settings
    [Category("Video Settings")]
    [PropertyVisibility(nameof(Mode), CameraMode.Video)]
    public int VideoFrameRate { get; set; } = 30;

    [Category("Video Settings")]
    [PropertyVisibility(nameof(Mode), CameraMode.Video)]
    public string VideoCodec { get; set; } = "H.264";

    // Timelapse settings
    [Category("Timelapse Settings")]
    [PropertyVisibility(nameof(Mode), CameraMode.Timelapse)]
    public int IntervalSeconds { get; set; } = 5;
}
```

### 4. Boolean Visibility (Original Feature - Still Supported)

The original boolean-based visibility still works as before:

```csharp
public class FeatureConfig
{
    [Category("General")]
    [DisplayName("Enable Advanced Features")]
    public bool IsAdvancedEnabled { get; set; } = false;

    // Visible only when IsAdvancedEnabled is true
    [Category("Advanced")]
    [DisplayName("Advanced Setting")]
    [PropertyVisibility(nameof(IsAdvancedEnabled))]
    public string AdvancedSetting { get; set; } = "Advanced";

    // Visible only when IsAdvancedEnabled is false (inverted)
    [Category("Basic")]
    [DisplayName("Basic Setting")]
    [PropertyVisibility(nameof(IsAdvancedEnabled), isInverted: true)]
    public string BasicSetting { get; set; } = "Basic";
}
```

### 5. Combining Boolean and Enum Visibility

You can use both boolean and enum visibility in the same class:

```csharp
public class MixedConfig
{
    public enum ProcessingMode
    {
        Fast,
        Quality,
        Custom
    }

    [Category("General")]
    public ProcessingMode Mode { get; set; } = ProcessingMode.Fast;

    [Category("General")]
    [DisplayName("Enable Custom Processing")]
    public bool UseCustomProcessing { get; set; } = false;

    // Visible when Mode is Custom
    [Category("Processing")]
    [DisplayName("Custom Algorithm")]
    [PropertyVisibility(nameof(Mode), ProcessingMode.Custom)]
    public string CustomAlgorithm { get; set; } = "Default";

    // Visible when UseCustomProcessing is true
    [Category("Processing")]
    [DisplayName("Custom Parameters")]
    [PropertyVisibility(nameof(UseCustomProcessing))]
    public string CustomParameters { get; set; } = "params";
}
```

## How It Works

### Boolean Visibility
- Uses `bool2VisibilityConverter` (normal) or `bool2VisibilityConverter1` (inverted)
- Property is visible when bound boolean is `true` (or `false` if inverted)

### Enum Visibility
- Uses `enum2VisibilityConverter` (normal) or `enum2VisibilityConverter1` (inverted)
- Property is visible when bound enum equals the specified `ExpectedValue`
- If inverted, property is hidden when enum equals the specified value

## Attribute Constructors

```csharp
// Boolean visibility
PropertyVisibilityAttribute(string propertyName, bool isInverted = false)

// Enum visibility
PropertyVisibilityAttribute(string propertyName, object expectedValue, bool isInverted = false)
```

## Parameters

- **propertyName**: Name of the property to bind to (must be a boolean or enum property)
- **expectedValue**: (Enum mode only) The enum value that makes the property visible
- **isInverted**: If `true`, inverts the visibility logic

## Notes

1. The property being bound to (`propertyName`) should implement `INotifyPropertyChanged` for dynamic updates
2. For enum visibility, the `expectedValue` must be of the same enum type as the bound property
3. The attribute only affects visibility in `PropertyEditorWindow` and property grid controls
4. Properties hidden by this attribute can still be accessed programmatically
5. Use meaningful enum names for better code readability

## Migration from Boolean-Only

Existing code using boolean visibility continues to work without changes:

```csharp
// Old code - still works
[PropertyVisibility(nameof(IsEnabled))]
public string MySetting { get; set; }

// New code - enum support
[PropertyVisibility(nameof(Mode), MyEnum.Value1)]
public string MySetting2 { get; set; }
```

## Real-World Example

Here's a complete example from a device configuration:

```csharp
using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices
{
    public class DeviceConfig : ViewModelBase
    {
        public enum ConnectionType
        {
            USB,
            Ethernet,
            Serial
        }

        [Category("Connection")]
        [DisplayName("Connection Type")]
        public ConnectionType Connection { get; set; } = ConnectionType.USB;

        // USB-specific settings
        [Category("Connection")]
        [DisplayName("USB Port")]
        [PropertyVisibility(nameof(Connection), ConnectionType.USB)]
        public string UsbPort { get; set; } = "COM1";

        // Ethernet-specific settings
        [Category("Connection")]
        [DisplayName("IP Address")]
        [PropertyVisibility(nameof(Connection), ConnectionType.Ethernet)]
        public string IpAddress { get; set; } = "192.168.1.100";

        [Category("Connection")]
        [DisplayName("Port")]
        [PropertyVisibility(nameof(Connection), ConnectionType.Ethernet)]
        public int Port { get; set; } = 8080;

        // Serial-specific settings
        [Category("Connection")]
        [DisplayName("Baud Rate")]
        [PropertyVisibility(nameof(Connection), ConnectionType.Serial)]
        public int BaudRate { get; set; } = 9600;

        [Category("Connection")]
        [DisplayName("Data Bits")]
        [PropertyVisibility(nameof(Connection), ConnectionType.Serial)]
        public int DataBits { get; set; } = 8;
    }
}
```

When `Connection` changes in the PropertyEditorWindow, only the relevant settings for that connection type will be visible.
