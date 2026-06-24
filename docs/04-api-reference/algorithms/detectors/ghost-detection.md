# Ghost Detection

本页只描述当前仓库里真实存在的 Ghost 检测接入链，不再维护“独立 `ghost-detection` 算法 API”式旧稿。

## 先记住

Ghost 检测不是独立公共算法包，而是 `ColorVision.Engine` 中 ARVR 模板族的一支。它由参数模板、WPF 运行面板、MQTT 命令、结果 DAO、图像叠加和 CSV 导出组成。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/DisplayGhost.xaml.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgResultGhostDao.cs`

如果只是想弄清 Ghost 当前如何配置、如何发送命令、如何显示结果，这几处已经覆盖主干。

## 当前主链

| 环节 | 当前实现 |
| --- | --- |
| 模板入口 | `TemplateGhost : ITemplate<GhostParam>`，`TemplateDicId = 7`，`Code = ghost` |
| 参数模型 | `Ghost_radius`、`Ghost_cols`、`Ghost_rows`、`Ghost_ratioH`、`Ghost_ratioL`，偏向点阵几何和灰度比例 |
| 算法宿主 | `AlgorithmGhost` 负责窗口、颜色、模板、设备和图像路径打包，不是本地图像处理内核 |
| 运行界面 | `DisplayGhost` 绑定参数，选择 `BLUE/GREEN/RED`，读取设备图像、本地图像和批次输入 |
| 命令链 | `SendCommand(...)` 打包 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam`、`Color`，发布 `Ghost` 事件 |

## 结果当前怎么处理

`ViewHandleGhost` 是当前结果显示链最关键的入口。它负责：

- 通过 `AlgResultGhostDao.Instance.GetAllByPid(...)` 加载结果明细
- 把结果列表接回 `ViewResultAlg`
- 根据 `GhostPixel` 和 `LedPixel` 在图像上绘制叠加点位
- 在左侧列表中展示 `LEDCenters`、`LEDBlobGray`、`GhostAverageGray`
- 导出 CSV

当前 Ghost 结果通过数据库结果模型、图像叠加和列表视图呈现，不是单次调用返回统一 JSON。

## 当前几个最容易写错的点

| 误区 | 正确判断 |
| --- | --- |
| 把它写成独立公共 API | 当前入口在 `Templates/ARVR/Ghost`，属于 ARVR 模板族 |
| 把 `AlgorithmGhost` 写成本地计算内核 | 它主要负责 UI、输入、模板和消息组装 |
| 套用通用缺陷检测参数表 | 当前参数面只有点阵半径、行列数和灰度比例上下限 |
| 期待单次调用返回示例 JSON | 真实输出链是 `ViewHandleGhost`、DAO、图像叠加和列表视图 |

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
2. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
3. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
4. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/DisplayGhost.xaml.cs`
5. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`
