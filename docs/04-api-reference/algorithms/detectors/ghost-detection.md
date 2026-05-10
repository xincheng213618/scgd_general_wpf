# Ghost Detection

本页只描述当前仓库里真实存在的 Ghost 检测接入链，不再维护“独立 `ghost-detection` 算法 API”式旧稿。

## 先看当前这页实际在讲什么

按当前源码状态，Ghost 检测不是一个独立公共算法包，而是 `ColorVision.Engine` 里 ARVR 模板族的一支。它当前由这几层组成：

- Ghost 参数模板
- Ghost 算法 UI 宿主
- 图像输入与颜色选择界面
- MQTT 命令打包
- 结果加载、叠加显示和 CSV 导出

因此这页真正要讲的是“主程序里 Ghost 是怎样被托管和运行的”，而不是虚构一套脱离宿主存在的 Process API。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/DisplayGhost.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgResultGhostDao.cs`

如果只是想弄清 Ghost 当前如何配置、如何发送命令、如何显示结果，这几处已经覆盖主干。

## 当前主链怎么跑

### 模板入口

`TemplateGhost` 是 Ghost 的参数模板入口。当前实现非常直接：

- 继承 `ITemplate<GhostParam>`
- `TemplateDicId = 7`
- `Code = ghost`

这说明 Ghost 当前走的是经典强类型参数模板链，而不是 JSON 模板或独立配置文件链。

### 参数模型

`GhostParam` 当前暴露的是一组针对鬼影点阵检测的参数，而不是旧稿里那种泛化的阈值、面积、形态学开关全集。当前能直接看到的核心字段包括：

- `Ghost_radius`
- `Ghost_cols`
- `Ghost_rows`
- `Ghost_ratioH`
- `Ghost_ratioL`

从字段命名和描述看，这套参数更偏向“待检测鬼影点阵”的几何与灰度约束，而不是任意图像缺陷检测器的通用参数表。

### 算法宿主

`AlgorithmGhost` 当前不是底层图像处理内核，而是一个 `DisplayAlgorithmBase` 派生的宿主类。它主要负责：

- 打开 `TemplateGhost` 的编辑窗口
- 提供 `DisplayGhost` 用户控件
- 维护当前颜色选择 `CVOLEDCOLOR`
- 把模板、颜色、设备信息和图像路径打包进消息

最终它会发布事件名为 `Ghost` 的消息，而不是对外暴露一个统一的 `ghost-detection` 调用接口。

### 输入与运行界面

`DisplayGhost` 是当前用户真正接触到的运行界面。它承担的工作比旧文档里的“输入图像 + 参数”更具体：

- 绑定 `TemplateGhost.Params`
- 提供 `BLUE`、`GREEN`、`RED` 三种 `CVOLEDCOLOR` 选择
- 从 `ServiceManager` 获取图像源设备
- 支持批次号、Raw/CIE 文件和本地图像三种输入路径
- 允许刷新设备侧 Raw/CIE 文件列表
- 允许直接在本地或设备侧打开图像

因此当前 Ghost 运行面本质上是一个带设备交互能力的 WPF 面板，不是纯算法函数入口。

### MQTT 命令链

`AlgorithmGhost.SendCommand(...)` 当前会打包这些信息：

- `ImgFileName`
- `FileType`
- `DeviceCode`
- `DeviceType`
- `TemplateParam`
- `Color`

然后构造 `MsgSend` 并发布 `Ghost` 事件。

这也说明当前 Ghost 计算真正的执行端并不在这个 UI 类内部，而是在消息链的另一侧。

## 结果当前怎么处理

`ViewHandleGhost` 是当前结果显示链最关键的入口。它负责：

- 通过 `AlgResultGhostDao.Instance.GetAllByPid(...)` 加载结果明细
- 把结果列表接回 `ViewResultAlg`
- 根据 `GhostPixel` 和 `LedPixel` 在图像上绘制叠加点位
- 在左侧列表中展示 `LEDCenters`、`LEDBlobGray`、`GhostAverageGray`
- 导出 CSV

和旧稿里那种“返回一个统一 JSON 结构体”不同，当前 Ghost 结果主要通过数据库结果模型、图像叠加和列表视图来呈现。

## 当前几个最容易写错的点

### 它不是独立公共 API

当前 Ghost 检测明确属于 ARVR 模板族的一部分，入口在 `Templates/ARVR/Ghost`，不是一个通用 `ghost-detection` 库。

### 算法类不是本地计算内核

`AlgorithmGhost` 当前主要负责窗口、输入、模板和消息组装。把它写成直接处理 `Mat` 的本地算法实现，会和真实代码不符。

### 参数面远比旧稿窄

当前 `GhostParam` 暴露的是点阵半径、行列数和灰度比例上下限，不存在旧文档里那套完整的阈值/面积/形态学大表。

### 结果展示依赖 UI 和结果处理器

真实输出链是 `ViewHandleGhost` + 结果 DAO + 图像叠加，而不是单次调用返回一份示例 JSON。

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/DisplayGhost.xaml.cs`
5. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`

## 继续阅读

- [ARVR 模板](../templates/arvr-template.md)
- [算法系统概览](../overview.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)