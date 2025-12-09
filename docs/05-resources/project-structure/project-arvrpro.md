# ProjectARVRPro 项目说明

面向 AR/VR 显示设备的专业测试解决方案，提供完整的光学测试流程、配方管理与结果分析能力。本文档概述项目定位、核心能力、依赖与推荐的开发调试方式。

## 🎯 功能定位与适用场景

- **专业级光学测试**：覆盖亮度、色度、棋盘格、畸变、MTF、光轴中心等测试场景。
- **大流程编排**：依托 FlowEngine 与模板系统，将多测试步骤串联为可视化、可配置的流程。
- **可裁剪执行**：通过 `ProcessMeta.IsEnabled` 控制是否执行具体测试步骤，便于按场景快速裁剪。
- **结果存储与复核**：支持客观结果入库、回放、导出以及复核展示。

## 🔍 核心能力速览

| 模块/流程            | 说明 |
| ------------------- | ---- |
| White255 / W25      | 白场/灰阶亮度测试与修正 |
| Red / Green / Blue  | 单色通道测试与参数修正 |
| Black               | 黑场表现与噪声测试 |
| Chessboard          | 棋盘格/几何精度测试 |
| Distortion          | 畸变与几何校正流程 |
| MTFHV               | 水平/垂直 MTF 分析 |
| OpticCenter         | 光轴中心定位与偏移分析 |
| ProcessMeta 编排    | 通过 `IsEnabled` 选择性执行流程，自动跳过禁用步骤 |

相关实现位于 `Projects/ProjectARVRPro/Process/`，其中仓库内的 `README_IsEnabled_Feature.md` 对可裁剪执行行为有详细说明。

## 🔌 依赖与集成方式

- **引用的程序集**：`ColorVision.Engine`、`ColorVision.Engine.Templates`（含 LargeFlow 模板）、`ColorVision.UI`
- **集成模式**：作为插件被主程序自动发现和加载，支持独立窗口模式与大流程模式。
- **配置与资产**：`PluginConfig/` 与 `Recipe/` 下的配置文件需要随插件一同发布；`ProcessMeta` 配置会持久化到 `ProcessMetas.json`。

## 🚀 构建与调试

```bash
# Windows x64 环境
dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj -p:Platform=x64
```

- 建议在 VS2022 打开仓库根目录下的 `scgd_general_wpf.sln` 进行调试。
- 调试大流程时，可在 ProcessManager 中勾选/取消 `IsEnabled` 以验证跳步行为。

## 📄 文档与后续优化

- 项目使用说明（本页）适用于快速了解模块与目录。
- 性能与体验优化路线请见 [ProjectARVRPro 优化计划](/02-developer-guide/performance/arvrpro-optimization)。
- 通用流程与模板说明可参考 [流程引擎概览](/01-user-guide/workflow/README) 与 [Templates 架构](/03-architecture/components/templates/design)。
