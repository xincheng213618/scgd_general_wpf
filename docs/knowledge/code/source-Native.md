---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# Native 源码知识

> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。

以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。

## Native/ 根目录与跨模块关联 {#module-4e6174697665}

- [系统要求](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  首次构建所需Windows x64、.NET与C++工具链，区分已有native DLL与干净克隆。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

## Native/include {#module-4e61746976652f696e636c756465}

- [FindLightArea 发光区定位模板](../../04-api-reference/algorithms/templates/find-light-area.md) — `algorithms.find-light-area`
  区分远端 FindLightArea 模板与本地原生亮区检测 RobustV2；四角点不等于成功，须核对置信度、失败原因和各调用层的结果契约。

- [ImageEditor 直接 native 分析](../../04-api-reference/algorithms/local-native-analysis.md) — `algorithms.local-native-analysis`
  ImageEditor直接native灯珠与P2分析：Ghost/旋转模板/双目标定、缺失计数与完成边界；区别Engine/MQTT模板和统一Runner。

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的CPU/CUDA调用、HImage显示和计时；自动模式不做失败回退，关窗不取消计算，GPU少量图片存在未修复的越界风险。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

## Native/opencv\_cuda {#module-4e61746976652f6f70656e63765f63756461}

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的CPU/CUDA调用、HImage显示和计时；自动模式不做失败回退，关窗不取消计算，GPU少量图片存在未修复的越界风险。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

## Native/opencv\_helper {#module-4e61746976652f6f70656e63765f68656c706572}

- [FindLightArea 发光区定位模板](../../04-api-reference/algorithms/templates/find-light-area.md) — `algorithms.find-light-area`
  区分远端 FindLightArea 模板与本地原生亮区检测 RobustV2；四角点不等于成功，须核对置信度、失败原因和各调用层的结果契约。

- [ImageEditor 直接 native 分析](../../04-api-reference/algorithms/local-native-analysis.md) — `algorithms.local-native-analysis`
  ImageEditor直接native灯珠与P2分析：Ghost/旋转模板/双目标定、缺失计数与完成边界；区别Engine/MQTT模板和统一Runner。

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的CPU/CUDA调用、HImage显示和计时；自动模式不做失败回退，关窗不取消计算，GPU少量图片存在未修复的越界风险。

- [系统要求](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  首次构建所需Windows x64、.NET与C++工具链，区分已有native DLL与干净克隆。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。
