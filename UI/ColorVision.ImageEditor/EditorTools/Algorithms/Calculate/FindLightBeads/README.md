# FindLightBeads：本地灯珠分析入口

本目录通过矩形右键或 `AlgorithmsCall` 菜单，直接调用 `OpenCVMediaHelper.M_FindLightBeads` 并向当前图像画布追加圆标注；不是 Engine 的远端 LED 模板，也不经过统一算法 Runner 的 provider 门禁。

完整参数、ROI、像素布局、JSON 计数与异步完成契约统一维护在[直接 native 分析](../../../../../../docs/04-api-reference/algorithms/local-native-analysis.md)。

运行前提与不可忽略的限制：

- 需要 Windows/x64、匹配的 `opencv_helper.dll` 及其 native 依赖。真实实现位于仓库 `Native/opencv_helper/`；当前 ImageEditor 从项目引用取得 Core，首次拉仓的 native 构建前提见[native 集成](../../../../../../docs/02-developer-guide/engine-development/opencv-integration.md)。
- `MissingCount` 是预期数量与亮点数量的差值，不等于 `BlackCenters` 点数；当前暗区循环只处理第一条轮廓，不能把输出当作完整缺失清单。
- 只有矩形菜单先求 ROI 与图像交集；直接 native 调用的越界 ROI 会回退全图。RGB、浮点量程、调色板和预乘 Alpha 没有统一平台的归一化保证。
- `Execute` 返回不表示标注完成。UI 追加红色亮点圆、黄色暗区候选圆，不显示已注释的统计框，也不自动保存源图或业务结果。

现存灯珠相关测试仅验证圆标注渲染和缩放/撤销，不代表 native 检测精度或完整 UI 链已通过。
