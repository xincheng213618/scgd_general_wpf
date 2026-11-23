---
knowledge_id: "delivery.native-testing"
knowledge_type: "guide"
status: "current"
summary: "原生调试的工具集、配置、运行 DLL 和现场样本核对。"
aliases: ["原生测试", "原生调试", "opencv_helper_test", "v145", "LNK2019", "MSB8020"]
code_paths: ["Native/opencv_helper/opencv_helper.vcxproj", "packages/OpenCV.Debug.x64.props", "packages/OpenCV.Release.x64.props", "packages/NativeCopy.targets"]
related: ["delivery.testing", "delivery.prerequisites", "engine.native-integration", "engine.opencv-helper-api", "algorithms.find-light-area", "algorithms.find-cross", "ui.image-fusion"]
---

# 原生 helper 测试与调试

从实际 `.vcxproj` 和导入的属性表核对工具集、架构及 OpenCV 版本。用 Visual Studio Developer PowerShell 构建，确认应用没有占用 `NativeCopy.targets` 要写入的运行目录。 [原生集成](./opencv-integration.md)

## 构建前提与配置选择

完整解决方案可能将 Debug 配置映射到 Release helper。逐步调试时直接构建目标项目并选择匹配的配置，启动后在 Visual Studio 的“模块”窗口核对实际加载的 DLL、PDB 和源码版本。

## 断点、扩展与故障处理

| 现象 | 先核对 |
| --- | --- |
| 找不到 `cl`、`msbuild` 或出现 `MSB8020` | Developer PowerShell、项目声明的工具集和 Windows SDK |
| `LNK2019` 或找不到导入库 | 项目引用、实际构建配置、输出目录及 OpenCV `.lib` |
| 找不到 helper 或 OpenCV DLL | 实际运行目录、进程搜索路径和传递依赖 |
| DLL 加载后断点未命中 | DLL 与 PDB 是否同一构建，是否受 Release 优化影响 |

## 发光区真实样本批量比较

从发光区实现定位同图分析工具。保留原图和人工复核的参考，并记录两者的身份和实际加载的 helper DLL。对照需区分技术错误、未处理样本和算法正常拒绝。遇到几何或判定差异时结合原图叠图复核。真实样本比较、量产节拍和现场验收分别记录。 [发光区契约](../../04-api-reference/algorithms/templates/find-light-area.md)
