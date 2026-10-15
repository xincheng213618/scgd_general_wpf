OLED/CUDA 内部版裁剪备份（2026-09-26）

适用范围
本次按内部自用版要求移除本地 OLED 算法与 CUDA runtime，不保留无用接口或占位实现。
普通 LED 检测、远端 CVOLED 服务、相机/光谱仪 SDK 及许可证逻辑不在本次裁剪范围内。

备份内容
before-removal.zip：移除前的逐字节快照。
  wpf/DLL/scgd_internal_dll/：相互匹配的 cvCamera.dll、cvoled.dll、cudart64_12.dll。
  wpf/Engine/：原 C# 绑定、专用枚举、工程复制/打包项和包说明。
  wpf/Scripts/ 与 wpf/SDK/：原共享文件清单。
  native/：外部 scgd_internal_dll 仓库本次涉及的源码/头文件/项目文件。
  installer/ColorVision.aip：本次移除前的安装器工程，已包含更早的 IKap/MIL 配置裁剪。
  wpf/docs/：原绑定契约与演进规划，供历史对照。
manifest.json：每项原路径、ZIP 内路径、字节数与 SHA-256。
native-removal.patch：仅本次对原生现有文件的修改对照；删除的 cvOled.h 另存于 ZIP。
  原项目混用 GBK、UTF-8 和 UTF-16，补丁保留原始字节；UTF-16 文件应使用 ZIP 快照对照。

已删除的接口与实现
原生导出：CM_LedCalInit、CM_LedCalFind、CM_LedCalComBine、
          CM_LedCalFindHighDensity、CM_FindHighIndensityLed。
相应内部函数、OLED 缓冲映射、空 mergeMat 实现、cvOled.h、CVLED_COLOR 枚举和 cvOled.lib 链接输入。
C#：CvOledDLL、CVOLED_ERROR、CVLED_COLOR；仓库没有这些接口的调用方。
交付：两个 DLL 的工程复制/打包项、仓库和 PluginKit 共享清单、外部 AIP 的文件/组件/Feature 行。

恢复方式
1. 先用 manifest.json 校验需要恢复的 ZIP 条目，解压到临时目录比较。
   快照包含当时工作树的其他改动，不要整体覆盖当前工程、文档或安装器。
2. 如需原生源码恢复，仅恢复本次移除的声明、实现、枚举、cvOled.h 和 cvOled.lib 链接输入，
   保留后续优化与其他改动，以 Release/x64 重建 cvCameraTCL.vcxproj。
   临时回退可使用 ZIP 中配套的三个 DLL；其中 cvCamera.dll 已包含此前的光谱插值优化。
3. 恢复需要的 C# 绑定/枚举，并将两个依赖 DLL 放回 DLL/scgd_internal_dll/。
   恢复 cvColorVision.csproj 的相应 None/CopyToOutputDirectory/Pack/PackagePath 元数据。
4. 同步 Scripts/shared_files.json、SDK/ColorVision.PluginKit/scripts/shared_files.json
   以及外部 ColorVision.aip 中对应的两组文件/组件/Feature 行。
   CameraTest 与其他包继续以工程声明的依赖为准，不需要从历史目录递归复制文件。
5. 验证 Release/x64 主程序构建、NuGet/安装器输入、原生导出和 OLED 真机路径，再交付。
   这份备份用于内部版本；对外版本仍须按绑定契约启用并验证许可证分支。

当前实现不导出以上五个函数；其余导出名称保留。删除导出会改变后续自动分配的序号，
不能把此 DLL 当作按序号导入的外部二进制的兼容替代品。当前工程按名称 P/Invoke。

当前契约：docs/04-api-reference/engine-components/cvColorVision.md。
本轮本地构建、隔离加载、依赖/导出/打包检查记录：.artifacts/oled-removal-20260926/。
本目录不参与默认运行输出、NuGet 包或安装包。
