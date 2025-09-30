# Pattern Plugin (图卡生成工具)

[![Version](https://img.shields.io/badge/version-1.0-blue.svg)](manifest.json)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](../../LICENSE)
[![ColorVision](https://img.shields.io/badge/ColorVision-Plugin-orange.svg)](../../README.md)

Pattern Plugin is a comprehensive test pattern generation tool for ColorVision. It provides multiple types of test patterns used for display calibration, measurement, and quality assessment.

## 🎯 Overview

The Pattern Plugin generates various types of test patterns essential for display testing, camera calibration, and optical measurement systems. It supports customizable resolutions, parameter templates, global import/export, real-time preview, and custom field of view configurations.

## ✨ Features

- **11 Pattern Types**: Comprehensive collection of test patterns
- **Custom Resolution**: Support for any resolution up to 8K
- **Template System**: Save, load, and share pattern configurations
- **Real-time Preview**: Instant pattern visualization
- **Field of View**: Customizable pattern positioning and sizing
- **Batch Generation**: Generate multiple patterns at once
- **Export Options**: Multiple export formats supported
- **Parameter Persistence**: Settings saved automatically

## 📋 Supported Pattern Types

### 1. 纯色 (Solid) - Solid Color Patterns
- **Purpose**: Basic color accuracy testing, gamma calibration
- **Parameters**: 
  - Color selection (RGB values)
  - Brightness levels
  - Color tags for identification
- **Use Cases**: White point calibration, black level testing, color uniformity

### 2. 隔行点亮 (Stripe) - Interlaced Stripe Patterns  
- **Purpose**: Response time testing, motion blur assessment
- **Parameters**:
  - Horizontal/Vertical orientation
  - Stripe width and spacing
  - Foreground/Background colors
  - Field of view positioning
- **Use Cases**: Pixel response measurement, crosstalk analysis

### 3. Ring - Concentric Ring Patterns
- **Purpose**: Lens distortion testing, focus accuracy
- **Parameters**:
  - Ring width and spacing
  - Center line options
  - Multiple ring configurations
  - Color customization
- **Use Cases**: Optical system calibration, distortion measurement

### 4. MTF - Modulation Transfer Function Patterns
- **Purpose**: Resolution and sharpness testing
- **Parameters**:  
  - Line thickness and length
  - Chart types (Four-line, Slanted Edge, BMW)
  - Multiple orientations
  - Custom line pair configurations
- **Use Cases**: Camera resolution testing, lens performance evaluation

### 5. 九点 (NineDot) - Nine-Point Alignment
- **Purpose**: Multi-point focus and alignment testing
- **Parameters**:
  - Dot size and spacing  
  - Grid positioning
  - Color selection
  - Field coverage options
- **Use Cases**: Multi-camera alignment, focus uniformity testing

### 6. 点阵 (Dot) - Dot Matrix Patterns
- **Purpose**: Pixel accuracy and alignment testing
- **Parameters**:
  - Dot radius and spacing
  - Row/Column count (auto or manual)
  - Color options
  - Adaptive sizing
- **Use Cases**: Pixel mapping, display calibration

### 7. 十字网格 (CrossGrid) - Cross Grid Patterns
- **Purpose**: Geometric accuracy and grid alignment
- **Parameters**:
  - Cross size and thickness
  - Grid spacing and count
  - Line colors and styles
  - Positioning controls
- **Use Cases**: Geometric calibration, measurement reference

### 8. 十字 (Cross) - Cross Patterns
- **Purpose**: Center alignment and positioning reference
- **Parameters**:
  - Horizontal/Vertical line width and length
  - Multiple cross positions
  - Color customization
  - Field positioning
- **Use Cases**: Optical axis alignment, center point reference

### 9. 棋盘格 (Checkerboard) - Checkerboard Patterns
- **Purpose**: Camera calibration and distortion correction
- **Parameters**:
  - Grid size (by count or cell size)
  - Cell dimensions
  - Alternating colors
  - Field of view scaling
- **Use Cases**: Camera calibration, 3D reconstruction

### 10. SFR - Spatial Frequency Response
- **Purpose**: Advanced resolution and frequency response testing
- **Parameters**:
  - Frequency ranges
  - Edge orientations
  - Contrast levels
  - Analysis regions
- **Use Cases**: Image quality assessment, lens testing

### 11. LinePairMTF - Advanced MTF Line Pairs
- **Purpose**: Detailed MTF analysis with line pair patterns
- **Parameters**:
  - Line pair density
  - Multiple orientations and angles
  - Custom spacing configurations
  - Advanced chart types
- **Use Cases**: High-precision resolution testing, optical bench measurements

## 🚀 Quick Start

### Basic Usage

1. **Open the Pattern Plugin**:
   ```
   ColorVision → Plugins → Pattern
   ```

2. **Select Pattern Type**:
   - Choose from the pattern list on the left
   - Configure parameters in the editor panel

3. **Set Resolution**:
   - Choose from common presets (1080p, 4K, 8K)
   - Or enter custom width/height values

4. **Generate Pattern**:
   - Click "Generate" to create the pattern
   - Preview appears in the main display area

5. **Export Pattern**:
   - Use "Export" to save as image file
   - Multiple formats supported (PNG, JPEG, BMP)

### Template Management

```csharp
// Save current configuration as template
PatternManager.SaveTemplate("MyCustomPattern", currentConfig);

// Load template
PatternManager.LoadTemplate("MyCustomPattern");

// Export template group
PatternManager.ExportTemplateGroup("MyTemplates.json");
```

## 🔧 Advanced Configuration

### Custom Resolutions

The plugin supports various standard and custom resolutions:

```csharp
// Common presets available
("3840x2160", 3840, 2160), // 4K UHD
("1920x1080", 1920, 1080), // Full HD
("1280x720", 1280, 720),   // HD
("1024x768", 1024, 768),   // XGA
("800x600", 800, 600),     // SVGA
("640x480", 640, 480)      // VGA
```

### Field of View Settings

Configure pattern positioning within the display area:

```csharp
// Center pattern in 80% of display area
config.FieldOfViewX = 0.8;
config.FieldOfViewY = 0.8;
```

### Color Configuration

All patterns support custom color schemes:

```csharp
// Set primary and secondary colors
config.MainBrush = Brushes.White;   // Primary color
config.AltBrush = Brushes.Black;    // Secondary color
config.MainBrushTag = "W";          // White tag
config.AltBrushTag = "K";           // Black tag
```

## 📚 API Reference

### Core Interfaces

```csharp
public interface IPattern
{
    ViewModelBase GetConfig();
    void SetConfig(string config);
    UserControl GetPatternEditor();
    Mat Gen(int height, int width);
    string GetTemplateName();
}
```

### Pattern Base Class

```csharp
public abstract class IPatternBase<T> : IPatternBase where T : ViewModelBase, new()
{
    public T Config { get; set; } = new T();
    public override ViewModelBase GetConfig() => Config;
    public override void SetConfig(string config);
    public abstract UserControl GetPatternEditor();
    public abstract Mat Gen(int height, int width);
    public override string GetTemplateName();
}
```

### Pattern Generation

```csharp
// Generate pattern with specific dimensions
Mat pattern = patternInstance.Gen(1920, 1080);

// Convert to display format
var bitmap = pattern.ToWriteableBitmap();
imageDisplay.SetImageSource(bitmap);
```

## 🛠️ Development

### Creating Custom Patterns

1. **Inherit from IPatternBase<T>**:

```csharp
[DisplayName("My Custom Pattern")]
public class CustomPattern : IPatternBase<CustomPatternConfig>
{
    public override UserControl GetPatternEditor() 
        => new CustomPatternEditor(Config);
    
    public override Mat Gen(int height, int width)
    {
        // Your pattern generation logic here
        Mat image = new Mat(height, width, MatType.CV_8UC3, Scalar.All(0));
        // ... generate pattern ...
        return image;
    }
    
    public override string GetTemplateName()
        => $"Custom_{Config.SomeParameter}_{DateTime.Now:HHmmss}";
}
```

2. **Create Configuration Class**:

```csharp
public class CustomPatternConfig : ViewModelBase, IConfig
{
    public SolidColorBrush MainBrush { get; set; } = Brushes.White;
    public int CustomParameter { get; set; } = 100;
    // ... other parameters ...
}
```

3. **Build and Register**:

```bash
# Build the plugin
msbuild Pattern.csproj /p:Configuration=Release

# The pattern will be automatically discovered and loaded
```

### Building from Source

```bash
# Build the entire plugin
"C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\msbuild.exe" ^
  ..\scgd_general_wpf.sln /t:Plugins\Pattern /p:Configuration=Release /p:Platform=x64

# Package plugin
python ..\Scripts\build_plugin.py -t Plugins -p Pattern
```

## 📖 Usage Examples

### Generating MTF Test Patterns

```csharp
// Create MTF pattern for resolution testing
var mtfPattern = new PatternLinePairMTF();
mtfPattern.Config.ChartType = ChartType.FourLinePair;
mtfPattern.Config.LineThickness = 2;
mtfPattern.Config.LineLength = 40;

// Generate 4K pattern
Mat pattern = mtfPattern.Gen(2160, 3840);
```

### Batch Pattern Generation

```csharp
// Generate multiple patterns for testing sequence
var patterns = new List<IPattern> 
{
    new PatternSolid { Config = { MainBrush = Brushes.White } },
    new PatternSolid { Config = { MainBrush = Brushes.Black } },
    new PatternCheckerboard { Config = { GridX = 8, GridY = 8 } },
    new PatternLinePairMTF { Config = { ChartType = ChartType.FourLinePair } }
};

foreach (var pattern in patterns)
{
    var mat = pattern.Gen(1080, 1920);
    SavePattern(mat, pattern.GetTemplateName());
}
```

## 🔍 Troubleshooting

### Common Issues

1. **Pattern Not Displaying**:
   - Check resolution settings
   - Verify color values are valid
   - Ensure sufficient memory for large patterns

2. **Template Loading Failed**:
   - Validate JSON format
   - Check file permissions
   - Verify template version compatibility

3. **Export Issues**:
   - Check output directory permissions
   - Verify image format support
   - Ensure sufficient disk space

### Performance Optimization

```csharp
// For large patterns, consider using ROI
Rect roi = new Rect(startX, startY, roiWidth, roiHeight);
pattern.CopyTo(fullImage[roi]);

// Dispose of Mat objects to free memory
using (var pattern = GeneratePattern())
{
    // Use pattern
} // Automatically disposed
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/new-pattern`)
3. Implement your pattern following the existing conventions
4. Add appropriate unit tests
5. Submit a pull request

### Code Style

- Follow C# naming conventions
- Use XML documentation for public APIs
- Implement proper disposal for OpenCV Mat objects
- Add DisplayName attributes for UI display

## 📄 License

This plugin is part of the ColorVision project and is licensed under the MIT License. See the main project license for details.

## 🆘 Support

- **Documentation**: [ColorVision Docs](../../docs/)
- **Issues**: [GitHub Issues](https://github.com/xincheng213618/scgd_general_wpf/issues)
- **Discussions**: [GitHub Discussions](https://github.com/xincheng213618/scgd_general_wpf/discussions)

---

**Note**: This plugin requires ColorVision version 1.3.12.21 or higher. Ensure your installation meets the minimum requirements before using advanced features.