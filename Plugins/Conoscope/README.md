# Conoscope

Conoscope 是 ColorVision 的 VAM/锥镜图像分析插件，提供 CVCIE 显示、关注点采样、色域/对比度分析与导出。插件身份及最低宿主要求见 `manifest.json`；发布版本来自 `Conoscope.csproj` 编译后 DLL 的 `FileVersion`。

完整契约只在[Conoscope 图像、采集与分析](../../docs/04-api-reference/plugins/standard-plugins/conoscope.md)维护，包括采集完成、CVCIE 分阶段加载、Mat 所有权、视图状态、设置和测试边界。

## 包内运行与操作前提

- 使用匹配版本的 Windows/x64 宿主、`net10.0-windows` 运行环境及 ColorVision.Engine、ColorVision.ImageEditor、ColorVision.Solution、cvColorVision 等依赖和所用 native 运行库。当前 `CVCommCore.*` / `MQTTMessageLib.*` 类型编入 cvColorVision；旧交付若仍引用独立同名程序集，须保留匹配 DLL。仅有 `Conoscope.dll` 不代表完整运行环境。
- 普通 CVCIE 分析不要求相机硬件；MVS 观察相机另需海康驱动与 `MvCameraControl.dll`。Ribbon 的测量采集使用 Engine Flow 或 `DeviceCamera`，不是这条观察相机链。
- 采集、设备操作、POI 数据库保存和文件导出各有副作用，须在相应授权范围内执行。Flow/相机报告成功、找到文件、图像首屏和完整 XYZ 就绪不能互相替代。
- 有效 `SolutionDir` 下构建会同时写宿主 Debug/Release 插件目录；本项目还向两套宿主根目录复制项目引用 DLL 及存在的 PDB。不要把它当作只影响当前插件配置的隔离构建。
- 正式插件发布通过仓库 `Scripts/package_plugin.bat Conoscope`，会构建、上传并清理本地包；必须另获发布授权，普通构建和测试不代表发布完成。

本 README 供源码与插件元数据入口使用。相对知识链接仅在匹配版本的完整源码仓库中有效；交付包未包含 `docs/` 时，应回到同版本仓库读取完整说明，不能把包内简述当作真机验收结果。
