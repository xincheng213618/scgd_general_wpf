# Pattern / 图卡生成功能

本页不再把 Pattern 写成一个“当前仓库里完整存在、可直接开发的标准插件”。按现有源码状态看，旧文档描述的那套独立 `Plugins/Pattern/` 插件实现已经无法在仓库中对上。

## 当前结论

从当前源码树看：

- `Plugins/` 目录下实际存在的是 `Conoscope/`、`EventVWR/`、`Spectrum/`、`SystemMonitor/`、`WindowsServicePlugin/` 等项目。
- 当前仓库里不存在 `Plugins/Pattern/` 目录，也没有对应的 `.csproj`、窗口、`manifest.json` 或独立插件入口实现。
- 旧文档里提到的 `IPattern`、`IPatternBase<T>`、`PatternManager`、批量生成接口等类型，在当前仓库源码中也没有可对应的真实定义。

因此这页现在只能作为“图卡相关能力在仓库里的剩余落点说明”，而不能继续充当独立插件 API 参考。

## 仓库里实际还能对上的相关代码

虽然独立 Pattern 插件项目缺失，但仓库里仍然保留了和 PG/图卡切换相关的两条代码线：

### `Engine/cvColorVision/PG.cs`

这部分是对 `cvCamera.dll` 中 PG 相关原生接口的 P/Invoke 封装，当前能明确看到的能力包括：

- `CM_InitPG`
- `CM_ConnectToPG`
- `CM_StartPG`
- `CM_StopPG`
- `CM_ReSetPG`
- `CM_SwitchUpPG`
- `CM_SwitchDownPG`
- `CM_SwitchFramePG`

这说明当前仓库里更可靠的“图卡相关能力”是 PG 设备控制，而不是一个自带图案编辑器的高层插件工程。

### `Engine/FlowEngineLib/PG/PGLoopNode.cs`

FlowEngine 侧还有 `PGLoopNode`，它会把循环节点中的 PG 参数转换为命令列表，再通过流程执行链下发：

- `开始` -> `CM_StartPG`
- `停止` -> `CM_StopPG`
- `重置` -> `CM_ReSetPG`
- `上` / `下` -> 切换图卡
- `指定` -> `CM_SwitchFramePG`

这更像“流程里控制 PG 设备切换图卡”的能力，而不是旧文档里那种本地生成 11 种图案并导出 PNG/JPEG/BMP 的完整插件。

## 为什么旧文档现在不能继续照用

旧页里有几类内容，当前都已经无法用源码证明：

- 声称存在独立 `Pattern` 插件项目和菜单入口。
- 声称存在 `IPattern` / `IPatternBase<T>` 这类扩展接口。
- 声称支持 11 种图案、本地模板管理、批量导出、预览优化等一整套高层功能。
- 给出了大量并不存在于当前仓库的示例 API 和扩展代码。

继续保留这些内容，只会让读者误以为当前仓库里还能直接找到对应实现。

## 当前更合理的理解方式

如果你现在在这个仓库里追“图卡”能力，优先应这样理解：

1. 先把它视为 PG 设备控制链的一部分，而不是独立 WPF 插件。
2. 先看 `cvColorVision/PG.cs`，确认底层能发哪些 PG 命令。
3. 再看 `FlowEngineLib/PG/PGLoopNode.cs`，理解流程里如何批量或循环切换 PG。
4. 如果还需要查更高层的图卡生成 UI，请先确认对应源码是否已经迁出仓库、位于其他项目，或只是文档残留。

## 当前已知的文档漂移

`Plugins/README.md` 里目前仍然列着 `Pattern`、`ScreenRecorder`、`ImageProjector`、`YoloObjectDetection` 等目录，但当前 `Plugins/` 源码树并没有对应项目。这说明插件索引本身也存在历史残留，阅读时需要特别谨慎。

## 继续阅读

- [Engine 组件总览](../../engine-components/README.md)
- [FlowEngineLib 架构](../../../03-architecture/components/engine/flow-engine.md)
- [算法系统概览](../../algorithms/overview.md)
