# Pattern Plugin (图卡生成工具)

[![Version](https://img.shields.io/badge/version-1.0-blue.svg)](manifest.json)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](../../docs/license.md)
[![ColorVision](https://img.shields.io/badge/ColorVision-Plugin-orange.svg)](../../README.md)

Pattern Plugin（图卡生成工具）是 ColorVision 的综合测试图案生成插件，为显示器校准、测量和质量评估提供多种类型的测试图案。

## 🎯 概述

Pattern Plugin 生成各种类型的测试图案，这些图案对于显示器测试、相机校准和光学测量系统至关重要。支持自定义分辨率、参数模板、全局导入/导出、实时预览以及自定义视场配置。

## ✨ 核心功能

- **11种图案类型**：全面的测试图案集合
- **自定义分辨率**：支持最高8K的任意分辨率
- **模板系统**：保存、加载和共享图案配置
- **实时预览**：即时图案可视化
- **视场控制**：可自定义图案定位和大小
- **批量生成**：一次生成多个图案
- **导出选项**：支持多种导出格式
- **参数持久化**：设置自动保存

## 📋 支持的图案类型

### 1. 纯色 (Solid)
- **用途**：基础颜色准确性测试、伽马校准
- **参数**： 
  - 颜色选择（RGB值）
  - 亮度级别
  - 用于识别的颜色标签
- **应用场景**：白点校准、黑电平测试、颜色均匀性

### 2. 隔行点亮 (Stripe)
- **用途**：响应时间测试、运动模糊评估
- **参数**：
  - 水平/垂直方向
  - 条纹宽度和间距
  - 前景/背景颜色
  - 视场定位
- **应用场景**：像素响应测量、串扰分析

### 3. 环形图案 (Ring)
- **用途**：镜头畸变测试、对焦精度
- **参数**：
  - 环宽度和间距
  - 中心线选项
  - 多环配置
  - 颜色自定义
- **应用场景**：光学系统校准、畸变测量

### 4. MTF图案 (MTF)
- **用途**：调制传递函数测试、分辨率和锐度测试
- **参数**：  
  - 线条厚度和长度
  - 图表类型（四线对、倾斜边缘、宝马图）
  - 多方向
  - 自定义线对配置
- **应用场景**：相机分辨率测试、镜头性能评估

### 5. 九点图案 (NineDot)
- **用途**：多点对焦和对齐测试
- **参数**：
  - 点大小和间距
  - 网格定位
  - 颜色选择
  - 视场覆盖选项
- **应用场景**：多相机对齐、对焦均匀性测试

### 6. 点阵图案 (Dot)
- **用途**：像素精度和对齐测试
- **参数**：
  - 点半径和间距
  - 行/列数（自动或手动）
  - 颜色选项
  - 自适应大小调整
- **应用场景**：像素映射、显示器校准

### 7. 十字网格 (CrossGrid)
- **用途**：几何精度和网格对齐
- **参数**：
  - 十字大小和厚度
  - 网格间距和数量
  - 线条颜色和样式
  - 定位控制
- **应用场景**：几何校准、测量参考

### 8. 十字图案 (Cross)
- **用途**：中心对齐和定位参考
- **参数**：
  - 水平/垂直线宽和长度
  - 多个十字位置
  - 颜色自定义
  - 视场定位
- **应用场景**：光轴对齐、中心点参考

### 9. 棋盘格 (Checkerboard)
- **用途**：相机标定和畸变校正
- **参数**：
  - 网格大小（按数量或单元格大小）
  - 单元格尺寸
  - 交替颜色
  - 视场缩放
- **应用场景**：相机标定、3D重建

### 10. SFR图案 (SFR)
- **用途**：高级分辨率和频率响应测试
- **参数**：
  - 频率范围
  - 边缘方向
  - 对比度级别
  - 分析区域
- **应用场景**：图像质量评估、镜头测试

### 11. 线对MTF (LinePairMTF)
- **用途**：线对图案的详细MTF分析
- **参数**：
  - 线对密度
  - 多方向和角度
  - 自定义间距配置
  - 高级图表类型
- **应用场景**：高精度分辨率测试、光学工作台测量

## 🚀 快速开始

### 基本使用

1. **打开 Pattern 插件**：
   ```
   ColorVision → 插件 → Pattern
   ```

2. **选择图案类型**：
   - 从左侧图案列表中选择
   - 在编辑器面板中配置参数

3. **设置分辨率**：
   - 从常用预设中选择（1080p、4K、8K）
   - 或输入自定义宽度/高度值

4. **生成图案**：
   - 点击"生成"按钮创建图案
   - 预览将显示在主显示区域

5. **导出图案**：
   - 使用"导出"保存为图像文件
   - 支持多种格式（PNG、JPEG、BMP）

### 模板管理

```csharp
// 将当前配置保存为模板
PatternManager.SaveTemplate("MyCustomPattern", currentConfig);

// 加载模板
PatternManager.LoadTemplate("MyCustomPattern");

// 导出模板组
PatternManager.ExportTemplateGroup("MyTemplates.json");
```

## 🔧 高级配置

### 自定义分辨率

插件支持各种标准和自定义分辨率：

```csharp
// 可用的常用预设
("3840x2160", 3840, 2160), // 4K UHD
("1920x1080", 1920, 1080), // Full HD
("1280x720", 1280, 720),   // HD
("1024x768", 1024, 768),   // XGA
("800x600", 800, 600),     // SVGA
("640x480", 640, 480)      // VGA
```

### 视场设置

配置图案在显示区域内的定位：

```csharp
// 在显示区域的80%中居中图案
config.FieldOfViewX = 0.8;
config.FieldOfViewY = 0.8;
```

### 颜色配置

所有图案都支持自定义颜色方案：

```csharp
// 设置主色和辅色
config.MainBrush = Brushes.White;   // 主色
config.AltBrush = Brushes.Black;    // 辅色
config.MainBrushTag = "W";          // 白色标签
config.AltBrushTag = "K";           // 黑色标签
```

## 📚 API 参考

### 核心接口

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

### 图案基类

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

### 图案生成

```csharp
// 生成指定尺寸的图案
Mat pattern = patternInstance.Gen(1920, 1080);

// 转换为显示格式
var bitmap = pattern.ToWriteableBitmap();
imageDisplay.SetImageSource(bitmap);
```

## 🛠️ 开发指南

### 创建自定义图案

1. **继承 IPatternBase<T>**：

```csharp
[DisplayName("自定义图案")]
public class CustomPattern : IPatternBase<CustomPatternConfig>
{
    public override UserControl GetPatternEditor() 
        => new CustomPatternEditor(Config);
    
    public override Mat Gen(int height, int width)
    {
        // 图案生成逻辑
        Mat image = new Mat(height, width, MatType.CV_8UC3, Scalar.All(0));
        // ... 生成图案 ...
        return image;
    }
    
    public override string GetTemplateName()
        => $"Custom_{Config.SomeParameter}_{DateTime.Now:HHmmss}";
}
```

2. **创建配置类**：

```csharp
public class CustomPatternConfig : ViewModelBase, IConfig
{
    public SolidColorBrush MainBrush { get; set; } = Brushes.White;
    public int CustomParameter { get; set; } = 100;
    // ... 其他参数 ...
}
```

3. **构建和注册**：

```bash
# 构建插件
msbuild Pattern.csproj /p:Configuration=Release

# 图案将被自动发现和加载
```

### 从源码构建

```bash
# 构建整个插件
"C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\msbuild.exe" ^
  ..\scgd_general_wpf.sln /t:Plugins\Pattern /p:Configuration=Release /p:Platform=x64

# 打包插件
python ..\Scripts\build_plugin.py -t Plugins -p Pattern
```

## 📖 使用示例

### 生成 MTF 测试图案

```csharp
// 创建用于分辨率测试的 MTF 图案
var mtfPattern = new PatternLinePairMTF();
mtfPattern.Config.ChartType = ChartType.FourLinePair;
mtfPattern.Config.LineThickness = 2;
mtfPattern.Config.LineLength = 40;

// 生成 4K 图案
Mat pattern = mtfPattern.Gen(2160, 3840);
```

### 批量生成图案

```csharp
// 为测试序列生成多个图案
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

## 🔍 故障排除

### 常见问题

1. **图案无法显示**：
   - 检查分辨率设置是否合理
   - 验证颜色值是否有效
   - 确保有足够内存用于大图案

2. **模板加载失败**：
   - 验证 JSON 格式是否正确
   - 检查文件权限
   - 确认模板版本兼容性

3. **导出问题**：
   - 检查输出目录权限
   - 验证图像格式支持
   - 确保有足够磁盘空间

### 性能优化

```csharp
// 对于大图案，考虑使用 ROI
Rect roi = new Rect(startX, startY, roiWidth, roiHeight);
pattern.CopyTo(fullImage[roi]);

// 释放 Mat 对象以释放内存
using (var pattern = GeneratePattern())
{
    // 使用图案
} // 自动释放
```

## 🤝 贡献指南

1. Fork 本仓库
2. 创建功能分支（`git checkout -b feature/new-pattern`）
3. 按照现有约定实现您的图案
4. 添加适当的单元测试
5. 提交 Pull Request

### 代码规范

- 遵循 C# 命名约定
- 为公共 API 使用 XML 文档注释
- 为 OpenCV Mat 对象实现正确的资源释放
- 为 UI 显示添加 DisplayName 属性

## 📄 许可证

本插件是 ColorVision 项目的一部分，采用 MIT 许可证。详见主项目许可证。

## 🆘 支持

- **文档**：[ColorVision 文档](../../docs/)
- **问题反馈**：[GitHub Issues](https://github.com/xincheng213618/scgd_general_wpf/issues)
- **讨论**：[GitHub Discussions](https://github.com/xincheng213618/scgd_general_wpf/discussions)

---

**注意**：此插件需要 ColorVision 版本 1.3.12.21 或更高版本。在使用高级功能之前，请确保您的安装满足最低要求。