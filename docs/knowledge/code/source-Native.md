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

- [系统要求与首次构建](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  Windows x64 运行与源码构建前提：Desktop Runtime、SDK、C++ 工具集及已有 native DLL 的选择。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

## Native/include {#module-4e61746976652f696e636c756465}

- [本地十字定位 FindCross](../../04-api-reference/algorithms/detectors/find-cross.md) — `algorithms.find-cross`
  本地十字定位的图像菜单、Flow 节点、生产参数、全图坐标、原生返回值与失败诊断。

- [发光区定位：远端模板与本地 V2](../../04-api-reference/algorithms/templates/find-light-area.md) — `algorithms.find-light-area`
  发光区定位1与本地发光区定位(V2)的使用、图像来源、POI保存模板和结果边界；区分算法拒绝、数据库提交与消息发布，并说明模板字典恢复不一致。

- [本地灯珠与 P2 分析](../../04-api-reference/algorithms/local-native-analysis.md) — `algorithms.local-native-analysis`
  ImageEditor 本地灯珠、Ghost、旋转模板和双目标定融合的操作、参数与结果；灯珠暗区候选不完整，P2 运行失败后复制结果可能仍取上次 JSON。

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的文件准备、CPU/CUDA执行、结果另存与计时；自动模式不做失败回退，关窗不取消计算，GPU的2–4张输入存在越界风险。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [opencv\_helper.dll API 参考](../../04-api-reference/engine-components/opencv-helper-api.md) — `engine.opencv-helper-api`
  opencv\_helper 英文 API 参考：校准/POI、图像处理、SFR、检测、视频与内存释放；核对真实参数单位和函数族错误码，声明的选项不等于当前 Engine 提供操作入口。

## Native/opencv\_cuda {#module-4e61746976652f6f70656e63765f63756461}

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的文件准备、CPU/CUDA执行、结果另存与计时；自动模式不做失败回退，关窗不取消计算，GPU的2–4张输入存在越界风险。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

## Native/opencv\_helper {#module-4e61746976652f6f70656e63765f68656c706572}

- [本地十字定位 FindCross](../../04-api-reference/algorithms/detectors/find-cross.md) — `algorithms.find-cross`
  本地十字定位的图像菜单、Flow 节点、生产参数、全图坐标、原生返回值与失败诊断。

- [发光区定位：远端模板与本地 V2](../../04-api-reference/algorithms/templates/find-light-area.md) — `algorithms.find-light-area`
  发光区定位1与本地发光区定位(V2)的使用、图像来源、POI保存模板和结果边界；区分算法拒绝、数据库提交与消息发布，并说明模板字典恢复不一致。

- [本地灯珠与 P2 分析](../../04-api-reference/algorithms/local-native-analysis.md) — `algorithms.local-native-analysis`
  ImageEditor 本地灯珠、Ghost、旋转模板和双目标定融合的操作、参数与结果；灯珠暗区候选不完整，P2 运行失败后复制结果可能仍取上次 JSON。

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的文件准备、CPU/CUDA执行、结果另存与计时；自动模式不做失败回退，关窗不取消计算，GPU的2–4张输入存在越界风险。

- [原生 helper 测试与调试](../../02-developer-guide/engine-development/native-testing.md) — `delivery.native-testing`
  opencv\_helper\_test 的实际入口、工具集与配置映射、专项参数、DLL/样本前提和退出码边界；默认运行与真实样本验收不同。

- [系统要求与首次构建](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  Windows x64 运行与源码构建前提：Desktop Runtime、SDK、C++ 工具集及已有 native DLL 的选择。

- [OpenCV 和 native 集成开发指南](../../02-developer-guide/engine-development/opencv-integration.md) — `engine.native-integration`
  native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。

- [成像校正：参考图、执行与结果保存](../../02-developer-guide/core-concepts/imaging-correction-v1.md) — `algorithms.imaging-correction`
  成像校正的参考图、固定阶段、参数/preset、执行并提交、mask与PNG/CSV/JSON保存；明确Alpha裁剪、无效样本、精确复制和批量只保存主图的边界。

- [opencv\_helper.dll API 参考](../../04-api-reference/engine-components/opencv-helper-api.md) — `engine.opencv-helper-api`
  opencv\_helper 英文 API 参考：校准/POI、图像处理、SFR、检测、视频与内存释放；核对真实参数单位和函数族错误码，声明的选项不等于当前 Engine 提供操作入口。
