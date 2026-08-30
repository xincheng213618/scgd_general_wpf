---
knowledge_id: "algorithms.ghost"
knowledge_type: "topic"
status: "current"
summary: "说明 ARVR Ghost 传统模板的参数、MQTT 事件、结果 DAO 和叠图。"
aliases: ["Ghost检测入口和结果在哪里","TemplateGhost","AlgorithmGhost","ViewHandleGhost"]
code_paths: ["Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs","Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs","Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs","Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs"]
test_paths: []
related: ["algorithms.arvr","algorithms.json-templates","engine.results"]
---

# Ghost Detection

本页只描述当前仓库里真实存在的 Ghost 检测接入链，不再维护“独立 `ghost-detection` 算法 API”式旧稿。

## 先记住

Ghost 检测不是独立公共算法包，而是 `ColorVision.Engine` 中 ARVR 模板族的一支。它由参数模板、通用显示算法配置、MQTT 命令、结果 DAO、图像叠加和 CSV 导出组成。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgResultGhostDao.cs`

如果只是想弄清 Ghost 当前如何配置、如何发送命令、如何显示结果，这几处已经覆盖主干。

## 当前主链

| 环节 | 当前实现 |
| --- | --- |
| 模板入口 | `TemplateGhost : ITemplate<GhostParam>`，`TemplateDicId = 7`，`Code = ghost` |
| 参数模型 | `Ghost_radius`、`Ghost_cols`、`Ghost_rows`、`Ghost_ratioH`、`Ghost_ratioL`，偏向点阵几何和灰度比例 |
| 算法宿主 | `AlgorithmGhost` 负责颜色、模板、设备和图像输入打包，不是本地图像处理内核 |
| 运行配置 | `GhostDisplayAlgorithmConfig` 在通用 `DisplayAlgorithmBase` 界面中提供 Ghost 模板和 `BLUE/GREEN/RED` 颜色选择 |
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
4. `Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs`

## 验证入口与缺口

验证缺口：未登记 Ghost 服务与结果 DAO 的专门自动化测试；需用已知图像、颜色和点阵参数核对真实请求、明细与叠图，不以页面示例作为通过证据。
