# Histogram Editing Feature - Testing Guide

## Overview
This directory contains tests for the histogram editing functionality added to ColorVision.ImageEditor.

## Feature Description
The histogram editing feature allows users to:
- View image histograms with interactive tone curve editing
- Drag control points to adjust the tone curve
- Apply real-time preview of changes to the image
- Support for both grayscale and RGB images

## Core Components

### 1. CurvePoint
Represents a single control point on the tone curve.
- Input value: 0-255 (original pixel value)
- Output value: 0-255 (adjusted pixel value)

### 2. ToneCurve
Manages the entire tone curve with:
- Multiple control points
- Linear interpolation between points
- Lookup table (LUT) generation for fast pixel remapping
- Add/remove/update control points

### 3. HistogramChartWindow
Enhanced WPF window with:
- Histogram visualization using ScottPlot
- Edit mode toggle
- Interactive curve drawing
- Mouse drag to adjust points
- Right-click to remove points
- Apply/Reset buttons

## Testing

### Unit Tests
The `ToneCurveTests.cs` file contains comprehensive unit tests for:
- Default linear curve behavior
- Adding and updating control points
- Interpolation between points
- Removing points
- Resetting the curve
- Input/output clamping
- Finding closest points
- Black/white point protection

### Running Tests (Windows Only)
These tests require Windows Desktop framework (.NET 8.0-windows) and can only run on Windows:

```bash
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj --filter "FullyQualifiedName~ToneCurve"
```

### Manual Testing
To manually test the histogram editing feature:

1. Build and run the ColorVision application on Windows
2. Open an image in the ImageEditor
3. Click the Histogram button in the toolbar
4. Click the "编辑模式" (Edit Mode) button
5. Click on the histogram to add control points
6. Drag points to adjust the curve
7. Right-click on points to remove them
8. Click "应用" (Apply) to commit changes
9. Click "重置曲线" (Reset Curve) to restore linear curve

## Usage Examples

### Darkening Midtones
1. Add a point at input=128
2. Drag it down to output=64
3. This darkens the middle gray values

### S-Curve (Increase Contrast)
1. Add a point at input=64, output=32 (darken shadows)
2. Add a point at input=192, output=224 (brighten highlights)
3. This creates an S-curve that increases contrast

### Brighten Image
1. Add a point at input=128
2. Drag it up to output=192
3. This brightens the midtones

## Supported Pixel Formats
The LUT application supports:
- Gray8 (8-bit grayscale)
- Bgr24 (24-bit RGB)
- Bgra32 (32-bit RGBA)
- Bgr32 (32-bit RGB)
- Rgb48 (48-bit RGB, 16-bit per channel)

## Technical Notes

### Linear Interpolation
The tone curve uses linear interpolation between control points. For more advanced curves (e.g., cubic spline), the interpolation method can be extended.

### Performance
- LUT generation: O(n) where n = number of control points
- Pixel remapping: O(1) per pixel using pre-computed LUT
- The LUT is updated whenever control points change

### Limitations
- Currently supports only luminosity adjustments (affects all channels equally)
- Per-channel curve editing is not yet implemented
- No support for histogram stretching/equalization through the curve editor

## Future Enhancements
Potential improvements:
1. Per-channel curve editing (separate curves for R, G, B)
2. Cubic spline interpolation for smoother curves
3. Preset curves (e.g., "Increase Contrast", "Brighten", "Film Look")
4. Export/import curve settings
5. Histogram matching
6. Auto-contrast adjustment
