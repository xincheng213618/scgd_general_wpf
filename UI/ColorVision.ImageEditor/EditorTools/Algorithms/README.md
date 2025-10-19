# 算法工具 (Algorithm Tools)

本目录包含 ImageEditor 的图像处理算法工具实现。

## 已实现的算法工具

### 1. 反相 (InvertEditorTool)
- **功能**: 将图像颜色反转
- **操作**: 右键菜单 -> 算法 -> 反相
- **特点**: 无需参数，立即执行

### 2. 自动色阶调整 (AutoLevelsAdjustEditorTool)
- **功能**: 自动调整图像的色阶分布
- **操作**: 右键菜单 -> 算法 -> 自动色阶调整
- **特点**: 切换按钮，可以开启/关闭效果

### 3. 白平衡调整 (WhiteBalanceEditorTool)
- **功能**: 调整图像的RGB通道平衡
- **操作**: 右键菜单 -> 算法 -> 白平衡调整...
- **特点**: 打开窗口，提供三个滑动条分别调整R、G、B通道
- **窗口功能**: 
  - 实时预览调整效果
  - 还原白平衡按钮
  - 应用：将调整应用到图像
  - 取消：放弃调整

### 4. 伽马校正 (GammaCorrectionEditorTool)
- **功能**: 对图像进行伽马校正
- **操作**: 右键菜单 -> 算法 -> 伽马校正...
- **特点**: 打开窗口，提供伽马值滑动条 (0-10)
- **窗口功能**:
  - 实时预览调整效果
  - 应用：将调整应用到图像
  - 取消：放弃调整

### 5. 亮度对比度调整 (BrightnessContrastEditorTool)
- **功能**: 调整图像的亮度和对比度
- **操作**: 右键菜单 -> 算法 -> 亮度对比度...
- **特点**: 打开窗口，提供两个滑动条
  - 亮度: -100 到 150
  - 对比度: -50 到 100
- **窗口功能**:
  - 实时预览调整效果
  - 应用：将调整应用到图像
  - 取消：放弃调整

### 6. 阈值处理 (ThresholdEditorTool)
- **功能**: 对图像进行二值化阈值处理
- **操作**: 右键菜单 -> 算法 -> 阈值处理...
- **特点**: 打开窗口，提供阈值滑动条 (0-65535)
- **说明**: 最大值根据图像位深自动调整
- **窗口功能**:
  - 实时预览调整效果
  - 应用：将调整应用到图像
  - 取消：放弃调整

### 7. 滤除摩尔纹 (RemoveMoireEditorTool)
- **功能**: 去除图像中的摩尔纹干扰
- **操作**: 右键菜单 -> 算法 -> 滤除摩尔纹
- **特点**: 无需参数，立即执行

## 架构说明

### 上下文菜单注册
所有算法工具通过 `AlgorithmsEditorToolContextMenu` 类注册到右键菜单系统中。该类实现 `IIEditorToolContextMenu` 接口，会被 `EditorToolFactory` 自动发现和加载。

### 工具类型
1. **简单工具** (InvertEditorTool, RemoveMoireEditorTool): 直接执行算法，无需用户输入
2. **切换工具** (AutoLevelsAdjustEditorTool): 支持开启/关闭的切换状态
3. **窗口工具** (WhiteBalance, GammaCorrection, BrightnessContrast, Threshold): 打开独立窗口提供参数调整

### 窗口设计
所有需要参数调整的工具都遵循相同的窗口设计模式：
- 使用 HandyControl 的 PreviewSlider 提供实时预览
- 通过 DebounceTimer 防抖处理，避免频繁计算
- 提供"应用"和"取消"按钮
- 应用时将处理结果保存到 ViewBitmapSource
- 取消时恢复原始图像

## 开发指南

添加新算法工具的步骤：
1. 在 Algorithms 目录创建 EditorTool 类
2. 如需窗口，创建对应的 Window.xaml 和 Window.xaml.cs
3. 在 AlgorithmsEditorToolContextMenu 中注册菜单项
4. 遵循现有的命名和结构规范

## 依赖说明

- ColorVision.Core: 提供 OpenCVMediaHelper 等图像处理功能
- ColorVision.Common: 提供 DebounceTimer 等工具类
- ColorVision.UI: 提供菜单系统和窗口工具
- HandyControl: 提供 UI 控件
