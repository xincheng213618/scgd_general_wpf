# 图像算法工具 (Image Algorithm Tools)

本文件夹包含了 ImageView 中图像处理算法的右键菜单实现。

## 功能概述

所有算法都可以通过右键菜单访问：**图像算法** -> 选择具体算法

## 实现的算法

### 1. 反相 (Invert)
- **类型**: 直接应用
- **文件**: `InvertEditorTool.cs`
- **功能**: 反转图像的颜色
- **使用方式**: 点击后立即应用

### 2. 自动色阶调整 (Auto Levels Adjust)
- **类型**: 直接应用
- **文件**: `AutoLevelsAdjustEditorTool.cs`
- **功能**: 自动调整图像的色阶
- **使用方式**: 点击后立即应用

### 3. 白平衡调整 (White Balance)
- **类型**: 窗口调整
- **文件**: `WhiteBalanceWindow.xaml/cs`
- **功能**: 调整图像的红、绿、蓝通道平衡
- **使用方式**: 
  - 打开窗口
  - 使用三个滑动条分别调整 RGB 通道 (范围: 0.5 - 2.0)
  - 实时预览效果
  - 点击"应用"保存更改或"取消"恢复原图

### 4. 伽马校正 (Gamma Correction)
- **类型**: 窗口调整
- **文件**: `GammaCorrectionWindow.xaml/cs`
- **功能**: 调整图像的伽马值
- **使用方式**:
  - 打开窗口
  - 使用滑动条调整伽马值 (范围: 0 - 10)
  - 实时预览效果
  - 点击"应用"保存更改或"取消"恢复原图

### 5. 亮度对比度调整 (Brightness & Contrast)
- **类型**: 窗口调整
- **文件**: `BrightnessContrastWindow.xaml/cs`
- **功能**: 调整图像的亮度和对比度
- **使用方式**:
  - 打开窗口
  - 使用两个滑动条分别调整亮度 (-100 到 150) 和对比度 (-50 到 100)
  - 实时预览效果
  - 点击"应用"保存更改或"取消"恢复原图

### 6. 阈值处理 (Threshold)
- **类型**: 窗口调整
- **文件**: `ThresholdWindow.xaml/cs`
- **功能**: 对图像进行阈值化处理
- **使用方式**:
  - 打开窗口
  - 使用滑动条调整阈值 (范围根据图像深度自动调整，最大 65535)
  - 实时预览效果
  - 点击"应用"保存更改或"取消"恢复原图

### 7. 去除摩尔纹 (Remove Moire)
- **类型**: 直接应用
- **文件**: `RemoveMoireEditorTool.cs`
- **功能**: 去除图像中的摩尔纹干扰
- **使用方式**: 点击后立即应用

## 技术实现

### 上下文菜单注册
- `AlgorithmsContextMenu.cs` - 实现 `IIEditorToolContextMenu` 接口
- 自动被 `IEditorToolFactory` 发现并注册到右键菜单系统
- 使用 `EditorContext` 访问 `ImageView` 实例

### 算法执行
- 直接应用的算法：创建工具实例并调用 `Execute()` 方法
- 需要参数调整的算法：打开 XAML 窗口，使用滑动条实时预览，支持应用/取消操作

### 实时预览机制
- 调整参数时使用 `DebounceTimer` 防抖，避免频繁计算
- 预览结果存储在 `ImageView.FunctionImage`
- 点击"应用"将预览结果保存到 `ViewBitmapSource`
- 点击"取消"恢复到原始的 `ViewBitmapSource`

## 使用示例

```csharp
// 在代码中直接调用算法
var invertTool = new InvertEditorTool(imageView);
invertTool.Execute();

// 或通过窗口调整参数
var gammaWindow = new GammaCorrectionWindow(imageView)
{
    Owner = Application.Current.GetActiveWindow()
};
gammaWindow.ShowDialog();
```

## 依赖

- `ColorVision.Core` - 图像处理核心功能 (OpenCVMediaHelper)
- `ColorVision.Common.Utilities` - 工具类 (DebounceTimer)
- `HandyControl` - UI 控件库 (PreviewSlider)
