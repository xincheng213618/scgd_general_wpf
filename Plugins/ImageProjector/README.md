# ImageProjector Plugin - 图片投影工具

> 版本: 1.0.0 | 目标框架: .NET 8.0 / .NET 10.0 Windows | 依赖: ColorVision.UI

## 🎯 功能定位

ImageProjector 是 ColorVision 的图片投影工具插件，支持将图片全屏投影到指定显示器上。适用于测试图案展示、色彩校准、多屏演示等场景。

## 主要功能点

### 多显示器支持
- **显示器选择** - 可视化显示器布局，支持选择任意显示器
- **主/副显示器** - 自动识别主显示器和副显示器
- **分辨率显示** - 显示各显示器的分辨率信息

### 图片管理
- **多图片管理** - 添加、删除、上移、下移图片列表
- **图片预览** - 主界面实时预览选中图片
- **批量添加** - 支持多选文件批量添加
- **重复检测** - 自动检测并跳过重复图片

### 投影控制
- **投影中切换** - 上一张/下一张按钮快速切换投影图片
- **显示模式** - 适应、拉伸、居中、填充四种显示模式
- **ESC快速关闭** - 按 ESC 键快速关闭投影窗口

### 配置持久化
- **图片列表** - 自动保存图片列表
- **选中项** - 记住上次选中的图片
- **显示器** - 记住上次使用的显示器
- **显示模式** - 记住上次使用的显示模式

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                   ImageProjector Plugin                       │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │ImageProjector│   │  Fullscreen │    │MonitorLayout│      │
│  │   Window    │    │ImageWindow  │    │  Control    │      │
│  │             │    │             │    │             │      │
│  │ • 图片列表  │    │ • 全屏显示  │    │ • 布局显示  │      │
│  │ • 显示器选  │    │ • 图片渲染  │    │ • 屏幕选择  │      │
│  │ • 模式选择  │    │ • ESC监听   │    │ • 分辨率    │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐                          │
│  │   Config    │    │    Item     │                          │
│  │             │    │             │                          │
│  │ • 配置持久  │    │ • 图片项    │                          │
│  │ • 显示模式  │    │ • 文件路径  │                          │
│  │ • 选中状态  │    │ • 文件名    │                          │
│  └─────────────┘    └─────────────┘                          │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 使用方式

### 基本使用流程

1. **添加图片**
   - 点击"添加"按钮选择图片文件（支持多选）
   - 支持格式：TIF, PNG, JPG, BMP

2. **选择显示器**
   - 在可视化布局中点击选择目标显示器

3. **选择显示模式**
   - **适应** - 保持宽高比，完整显示图片（默认）
   - **拉伸** - 拉伸填满整个屏幕，可能变形
   - **居中** - 原始尺寸居中显示，不缩放
   - **填充** - 保持宽高比填满屏幕，可能裁剪

4. **开始投影**
   - 点击"投影"按钮开始全屏投影

5. **投影中切换**
   - 使用"上一张"/"下一张"按钮切换图片

6. **停止投影**
   - 点击"停止投影"按钮或按 ESC 键关闭

### 快捷键

| 快捷键 | 功能 |
|--------|------|
| Delete | 删除选中的图片 |
| Ctrl + A | 全选图片 |
| ESC | 关闭投影窗口 |

## 主要组件

### ImageProjectorWindow
主窗口类，提供图片管理和投影控制界面。

```csharp
public partial class ImageProjectorWindow : Window, IDisposable
{
    // 全屏投影窗口实例
    private FullscreenImageWindow? _fullscreenWindow;
    
    // 开始投影
    private void Project_Click(object sender, RoutedEventArgs e);
    
    // 停止投影
    private void Stop_Click(object sender, RoutedEventArgs e);
    
    // 上一张/下一张
    private void Previous_Click(object sender, RoutedEventArgs e);
    private void Next_Click(object sender, RoutedEventArgs e);
}
```

### ImageProjectorConfig
配置类，存储图片投影器的设置。

```csharp
public class ImageProjectorConfig : ViewModelBase, IConfig
{
    public static ImageProjectorConfig Instance => 
        ConfigService.Instance.GetRequiredService<ImageProjectorConfig>();
    
    // 图片列表
    public ObservableCollection<ImageProjectorItem> ImageItems { get; set; }
    
    // 上次选中的索引
    public int LastSelectedIndex { get; set; }
    
    // 上次选中的显示器
    public string LastSelectedMonitor { get; set; }
    
    // 图片显示模式
    public ImageStretchMode StretchMode { get; set; }
}
```

### ImageStretchMode
图片显示模式枚举。

```csharp
public enum ImageStretchMode
{
    [Description("适应")]
    Uniform,        // 保持宽高比，完整显示图片
    [Description("拉伸")]
    Fill,           // 拉伸填满整个屏幕
    [Description("居中")]
    None,           // 原始尺寸居中显示
    [Description("填充")]
    UniformToFill   // 保持宽高比填满屏幕
}
```

## 目录说明

- `ImageProjectorWindow.xaml/cs` - 主窗口
- `FullscreenImageWindow.xaml/cs` - 全屏投影窗口
- `ImageProjectorConfig.cs` - 配置类
- `ImageProjectorItem.cs` - 图片项模型
- `MenuImageProjector.cs` - 菜单项
- `MonitorLayoutControl.cs` - 显示器布局控件

## 开发调试

```bash
# 构建项目
dotnet build Plugins/ImageProjector/ImageProjector.csproj

# 运行测试
dotnet test
```

## 最佳实践

### 1. 图片准备
- 使用与目标显示器分辨率匹配的图片
- 提前将测试图片整理到同一目录
- 使用有意义的文件名便于识别

### 2. 显示模式选择

| 场景 | 推荐模式 |
|------|----------|
| 色彩校准 | 居中 |
| 测试图案展示 | 适应或填充 |
| 全屏演示 | 填充 |

### 3. 快捷键使用
- 使用 Delete 键快速删除图片
- 使用 Ctrl+A 全选批量删除
- 使用 ESC 键快速关闭投影

## 相关文档链接

- [详细技术文档](../../docs/04-api-reference/plugins/standard-plugins/image-projector.md)
- [ColorVision.UI README](../ColorVision.UI/README.md)

## 更新日志

### v1.0.0（2026-02）
- 初始版本发布
- 多显示器选择支持
- 图片列表管理
- 四种显示模式
- 配置持久化

## 维护者

ColorVision 插件团队
