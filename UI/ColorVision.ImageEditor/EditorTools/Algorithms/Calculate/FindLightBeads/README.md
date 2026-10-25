# FindLightBeads：本地灯珠分析入口

本目录通过矩形右键或“分析测量 → 定位与几何 → 灯珠定位”菜单，复用 Core 灯珠定位与完整网格检测，显示统计、逐点状态和 CSV 导出，并向当前图像画布追加批量图层；不是 Engine 的远端 LED 模板，也不经过统一算法 Runner 的 provider 门禁。

完整参数、ROI、像素布局、JSON 计数与异步完成契约统一维护在[直接 native 分析](../../../../../../docs/04-api-reference/algorithms/local-native-analysis.md)。

运行前提与不可忽略的限制：

- 需要 Windows/x64、匹配的 `opencv_helper.dll` 及其 native 依赖。真实实现位于仓库 `Native/opencv_helper/`；当前 ImageEditor 从项目引用取得 Core，首次拉仓的 native 构建前提见[native 集成](../../../../../../docs/02-developer-guide/engine-development/opencv-integration.md)。
- `MissingCount` 是预期数量与亮点数量的差值，不等于 `BlackCenters` 点数；当前暗区循环只处理第一条轮廓，不能把输出当作完整缺失清单。
- 托管包装拒绝越界 ROI，输出绝对源像素坐标；直接 native 调用的越界 ROI 仍会回退全图。RGB、浮点量程、调色板和预乘 Alpha 没有统一平台的归一化保证。
- `ExecuteAsync` 可等待；不良检测使用完整规则网格，不把 native 暗区启发式候选当作完整坏点。自动行列数无法保证覆盖全暗边缘，用户可输入已知行列数校验。检测不改写源图，导出由用户明确点击。

共享测试覆盖网格判定、运行期点集、POI 测量与批量绘制的生命周期，不代表所有屏幕的检测精度或完整 UI 链已通过。
