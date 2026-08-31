# ImageEditor 算法入口

本目录是图像编辑器的菜单、参数窗口与本地分析适配器，不是统一的 native 算法实现目录，也不是所有已实现算法的默认发布清单。

## 按执行链定位

- 普通像素变换：`AlgorithmsContextMenu` 从当前 Runtime/Catalog 投影菜单，工具和预览窗口使用 `ImageProcessingContext`，经 Invocation/Runner 执行。当前参数、格式、provider 门禁、预览/提交与取消见[统一图像算法平台](../../../../docs/02-developer-guide/core-concepts/image-algorithm-platform-v1.md)（`algorithms.platform`）。
- `Calculate/FindLightBeads` 与 `Calculate/P2`：直接调用 native 的本地分析入口，不应套用远端模板或统一 Runner 的全部保证。灯珠计数、Ghost、旋转模板与双目标定调试边界见[本地 native 分析](../../../../docs/04-api-reference/algorithms/local-native-analysis.md)（`algorithms.local-native-analysis`）。
- 图像打开、普通绘图、source/rendered 保存与结果叠加的区别见[ImageEditor 契约](../../../../docs/04-api-reference/ui-components/ColorVision.ImageEditor.md)。Engine 远端模板另从[算法与模板知识入口](../../../../docs/04-api-reference/algorithms/README.md)定位。

## 使用前先确认

“应用”普通像素预览只提交当前内存图，不会自动保存源文件；`async void Execute()` 返回也不是计算完成信号。取消只处理所属会话，不能恢复掉后续换图或其他算法的提交。

菜单是否出现和可执行取决于具体发现链、上下文、格式、发布及依赖检查，不能根据此目录中的类名自行补造产品入口。阈值窗口当前采用 0–255 标称刻度，非 8-bit 中值滤波也有核大小限制；不要沿用旧教程的按位深阈值范围或无条件支持承诺。

本地分析需要匹配的 native DLL/ABI；P2 双目窗口生成的标定仅供调试，不可直接用于真实测量。查看源码无需启动应用、连接设备、运行图片集合或写文件。这些链接面向匹配版本的完整源码仓库，不保证文档随独立程序集交付。
