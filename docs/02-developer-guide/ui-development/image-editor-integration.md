# 图像编辑器集成指南

本文档介绍如何与 ColorVision.ImageEditor 的 3D 视图功能集成，以及性能优化技巧。

## 目录
1. [快速开始](#快速开始)
2. [3D 视图集成](#3d-视图集成)
3. [性能优化](#性能优化)
4. [API 参考](#api-参考)
5. [最佳实践](#最佳实践)

---

## 快速开始

### 创建 3D 视图

```csharp
using ColorVision.ImageEditor;
using System.Windows.Media.Imaging;

// 从 WriteableBitmap 创建 3D 视图
WriteableBitmap bitmap = GetBitmap(); // 你的图像源
var window3D = new Window3D(bitmap)
{
    Owner = Application.Current.GetActiveWindow()
};
window3D.Show();
```

### 配置渲染参数

```csharp
// 通过配置文件设置默认分辨率
Window3D.Config.TargetPixelsX = 512;
Window3D.Config.TargetPixelsY = 512;
Window3D.Config.SelectedColormap = "jet"; // 默认伪彩色
```

---

## 3D 视图集成

### 自定义工具按钮

如果你想在图像编辑器工具栏添加自定义 3D 视图入口：

```csharp
// 实现 IEditorTool 接口
public record class MyCustom3DTool(EditorContext EditorContext) : IEditorTool
{
    public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
    public string? GuidId => "MyCustom3D";
    public int Order { get; set; } = 30;
    public object Icon { get; set; } = "3D";

    public ICommand? Command { get; set; } = new RelayCommand(_ =>
    {
        if (EditorContext.DrawCanvas.Source is WriteableBitmap writeableBitmap)
        {
            var window3D = new Window3D(writeableBitmap)
            {
                Owner = Application.Current.GetActiveWindow()
            };
            window3D.Show();
        }
    });
}
```

### 扩展 3D 视图功能

通过继承 `Window3D` 可以添加自定义功能：

```csharp
public class CustomWindow3D : Window3D
{
    public CustomWindow3D(WriteableBitmap bitmap) : base(bitmap)
    {
        // 自定义初始化
        Title = "自定义 3D 视图";
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        // 添加自定义灯光、模型等
    }
}
```

---

## 性能优化

### 1. 网格缓存策略（v2.0+）

3D 视图采用智能网格缓存策略，大幅提升高度缩放性能：

```csharp
// 内部实现：只更新顶点位置，不重建 Mesh
private void UpdateMeshPositions()
{
    if (currentMesh == null || grayPixels == null) return;

    // 复用现有的 TriangleIndices 和 TextureCoordinates
    // 只更新 Positions 集合
    var newPositions = new Point3DCollection(vertexCount);
    for (int y = 0; y < newHeight; y++)
    {
        for (int x = 0; x < newWidth; x++)
        {
            double z = grayPixels[idx] / 255.0 * heightScale;
            newPositions.Add(new Point3D(x, flippedY, z));
        }
    }
    currentMesh.Positions = newPositions;
}
```

**性能对比：**
- 原方案：每次缩放重建整个 Mesh（26万顶点 + 150万索引）≈ 50-100ms
- 优化后：只更新顶点位置 ≈ 5-10ms
- **提升：5-10倍**

### 2. 分辨率选择

根据图像大小选择合适的渲染分辨率：

| 原始图像 | 建议目标分辨率 | 说明 |
|----------|---------------|------|
| < 1MP | 256x256 或 512x512 | 细节足够，性能最佳 |
| 1-4MP | 512x512 | 平衡质量和性能 |
| 4-16MP | 512x512 | 大图像需要下采样 |
| > 16MP | 256x256 或 512x512 | 避免内存和渲染压力 |

### 3. 资源冻结

确保大型资源在 UI 线程上冻结：

```csharp
// 伪彩色映射纹理
var image = new BitmapImage(uri);
image.Freeze(); // 关键！允许跨线程访问

// 材质
var brush = new ImageBrush(bitmap);
brush.Freeze();
var material = new DiffuseMaterial(brush);
```

### 4. 异步初始化

```csharp
private async void Window_Initialized(object sender, EventArgs e)
{
    // 耗时操作在后台线程执行
    var (grayPixels, newWidth, newHeight) = await Task.Run(() => 
        ConvertBitmapToGray(colorBitmap, Config.TargetPixelsX, Config.TargetPixelsY)
    );

    // 回到 UI 线程创建 3D 资源
    await BuildMeshAsync();
}
```

---

## API 参考

### Window3D

```csharp
public partial class Window3D : Window
{
    // 构造函数
    public Window3D(WriteableBitmap writeableBitmap);

    // 配置（静态）
    public static Window3DConfig Config { get; }

    // 截图功能
    private void SaveScreenshot();

    // 重置相机
    private void ResetCameraView();

    // 网格更新（内部）
    private async Task BuildMeshAsync();      // 首次构建
    private void UpdateMeshPositions();        // 高度缩放更新
}
```

### Window3DConfig

```csharp
public class Window3DConfig : ViewModelBase, IConfig
{
    public static Window3DConfig Instance { get; }

    // 目标分辨率（默认 512x512）
    public int TargetPixelsX { get; set; }
    public int TargetPixelsY { get; set; }

    // 默认伪彩色映射（默认 "jet"）
    public string SelectedColormap { get; set; }
}
```

### ColormapInfo

```csharp
public record ColormapInfo(
    string Name,           // 映射名称
    BitmapImage? ImageSource,  // 颜色条预览图
    byte[]? Lut              // 查找表数据
);
```

---

## 最佳实践

### 1. 错误处理

```csharp
public ICommand? Command { get; set; } = new RelayCommand(_ =>
{
    try
    {
        if (EditorContext.DrawCanvas.Source is WriteableBitmap writeableBitmap)
        {
            var window3D = new Window3D(writeableBitmap)
            {
                Owner = Application.Current.GetActiveWindow()
            };
            window3D.Show();
        }
        else
        {
            MessageBox.Show("当前没有可显示的图像", "提示");
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"打开 3D 视图失败: {ex.Message}", "错误");
    }
});
```

### 2. 资源清理

确保窗口关闭时正确释放资源：

```csharp
private void Window_Closed(object sender, EventArgs e)
{
    // 移除事件处理器
    PreviewKeyDown -= Window3D_PreviewKeyDown;
    viewport.MouseMove -= Viewport_MouseMove;
    viewport.MouseLeave -= Viewport_MouseLeave;

    // 清理 3D 资源
    viewport.Children.Clear();
    ContentGrid.Children.Remove(viewport);
    modelVisual.Content = null;

    // 释放引用
    currentMesh = null;
    colormapMaterial = null;
    grayPixels = null;
}
```

### 3. 键盘快捷键

3D 视图支持以下快捷键：

| 按键 | 功能 |
|------|------|
| `+` / `Num+` | 增加高度缩放 |
| `-` / `Num-` | 减少高度缩放 |
| `L` | 相机左移 |
| `T` | 相机前进 |
| `R` | 相机右移 |
| `B` | 相机后退 |
| `A` | 向上看 |
| `C` | 向下看 |
| `D` | 向左看 |
| `F` | 向右看 |
| `Home` | 重置视角 |

---

## 相关资源

- [图像编辑器用户指南](../../01-user-guide/image-editor/overview.md)
- [ColorVision.ImageEditor API 参考](../../04-api-reference/ui-components/ColorVision.ImageEditor.md)
- [UI 开发指南](./README.md)
- [性能优化指南](../performance/overview.md)
