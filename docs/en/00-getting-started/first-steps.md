# First Run Guide

This guide helps you understand how to launch the program and perform basic configuration after installing ColorVision for the first time.

## Launching the Program

### Method 1: Launch from Start Menu

1. Open the Windows Start menu
2. Find "ColorVision" or "视彩光电"
3. Click the icon to launch the program

### Method 2: Launch from Desktop Shortcut

If you chose to create a desktop shortcut during installation, double-click the ColorVision icon on the desktop to launch.

### Method 3: Launch from Command Line (Developers)

```powershell
cd "Install Directory"
.\ColorVision.exe
```

## Automatic Configuration on First Launch

The program automatically performs the following actions on first launch:

1. **Create Configuration Files**
   - Location: `%USERPROFILE%\Documents\ColorVision\`
   - Includes: user configuration, device configuration, workflow configuration, etc.

2. **Initialize Logging System**
   - Log directory: `%USERPROFILE%\Documents\ColorVision\logs\`
   - Log level: defaults to Info

3. **Scan Plugins**
   - Automatically scans the `Plugins` directory
   - Loads all available plugins

4. **Display Main Window**
   - Shows the main interface
   - Shows a welcome prompt (optional)

## Basic Interface Tour

### Main Window Layout

```
┌─────────────────────────────────────────────┐
│  Menu Bar                                    │
├─────────────────────────────────────────────┤
│  Toolbar                                     │
├──────────┬──────────────────────┬───────────┤
│          │                      │           │
│  Device  │   Image Display      │  Property │
│  List    │   Area               │  Panel    │
│          │                      │           │
│          │                      │           │
├──────────┴──────────────────────┴───────────┤
│  Status Bar                                  │
└─────────────────────────────────────────────┘
```

### Main Area Descriptions

- **Menu Bar**: File, Edit, View, Devices, Workflows, Plugins, Help, etc.
- **Toolbar**: Quick access buttons for common functions
- **Device List**: Displays connected and available devices
- **Image Display Area**: Displays images, workflow editor, etc.
- **Property Panel**: Displays properties of the selected object
- **Status Bar**: Displays program status and prompt messages

For detailed interface instructions, see: [Main Window Tour](../01-user-guide/interface/main-window.md)

## Basic Operation Demo

### 1. Open an Image

**Steps**:
1. Click **File** → **Open Image** in the menu bar
2. Or use the shortcut `Ctrl+O`
3. Select an image file (supports BMP, JPG, PNG, TIFF, etc.)
4. Click "Open"

The image will be displayed in the central display area.

### 2. Add a Simulated Device

**Steps**:
1. Click **Devices** → **Add Device** in the menu bar
2. Select **Simulated Camera** or another simulated device
3. Configure device parameters
4. Click "OK"

The device will appear in the device list on the left.

### 3. View Logs

**Steps**:
1. Click **Help** → **Logs** in the menu bar
2. Or use the shortcut `Ctrl+L`
3. View program runtime logs in the log window

### 4. View Loaded Plugins

**Steps**:
1. Click **Plugins** → **Plugin Manager** in the menu bar
2. View the list of loaded plugins
3. Enable/disable plugins as needed

## Common First-Run Issues

### Issue 1: Slow Program Startup

**Cause**: Initial configuration, plugin scanning, etc. need to be performed on first launch.

**Solution**:
- A normal first launch takes 10-30 seconds
- If it exceeds 1 minute, check the logs for the specific cause
- It may be a plugin loading issue; try temporarily removing plugins from the Plugins directory

### Issue 2: Abnormal UI Display

**Cause**: Resolution or DPI setting mismatch.

**Solution**:
- Recommended resolution: 1920x1080 or higher
- DPI scaling set to 100%
- Adjust display scaling in Windows Settings

### Issue 3: Configuration File Not Found

**Cause**: Permission issues or path issues.

**Solution**:
- Check if the user documents directory is accessible
- Run the program as administrator
- Check log for error messages

### Issue 4: Plugin Loading Failure

**Cause**: Missing plugin dependencies or version mismatch.

**Solution**:
- Check the log for plugin loading errors
- Check the plugin's manifest.json file
- Ensure the plugin is compatible with the ColorVision version

For more issues, see: [Troubleshooting](../01-user-guide/troubleshooting/common-issues.md)

## Next Steps

After completing the first run, we recommend the following learning order:

1. **Familiarize with the UI**: [Main Window Tour](../01-user-guide/interface/main-window.md)
2. **Learn Image Operations**: [Image Editor](../01-user-guide/image-editor/overview.md)
3. **Understand Device Usage**: [Device Overview](../01-user-guide/devices/overview.md)
4. **Try Workflows**: [Workflow Overview](../01-user-guide/workflow/README.md)

## Technical Support

If you encounter issues:
- See [Common Issues](../01-user-guide/troubleshooting/common-issues.md)
- Submit a [GitHub Issue](https://github.com/xincheng213618/scgd_general_wpf/issues)
- View the [Full Documentation](https://xincheng213618.github.io/scgd_general_wpf/)

---

**Tip**: Keep your network connection active during the first run so the program can check for updates and download necessary resources.