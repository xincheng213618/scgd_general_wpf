IKap / MIL 采集配置归档
======================

归档日期：2026-09-26

这四个文件原先由 cvColorVision 工程复制到 cfg_files，并随主安装包、
CameraTest 独立包一起交付。当前项目主要使用 HK 相机，因此将这组
设备专用配置移出默认构建与打包清单，保留原始内容以供需要时恢复。
本归档不是运行时目录，不应整体复制到程序输出或安装包。

内容
----
IKap/510.vlcf
  IKap 采集配置，包括图像尺寸、位深、时钟、触发等参数。
mil-dcf/ConfigDCF.ini
  8bit、12bit 对应 DCF 文件名的映射。
mil-dcf/VP101_85Mhz_10tap8bit-trigger-good.dcf
mil-dcf/VP101_85Mhz_4tap12bit-trigger-good.dcf
  MIL/Matrox 采集卡配置。它们面向特定设备，不能保证适用于其它型号。

manifest.json 记录每个文件的原始路径、归档路径、字节数和 SHA-256。
归档时已逐文件核对校验值，文件内容未修改。

按设备恢复
----------
1. 确认实际设备采用 IKap 或 MIL，并核对型号、位深和采集卡配置。
2. 按 manifest.json 的 original_path 将需要的文件复制回原位置。
   MIL 的 ConfigDCF.ini 和两个 DCF 应配套恢复；若使用其它配置，
   同步调整 ConfigDCF.ini，避免指向不存在或不匹配的文件。
3. 恢复 Engine/cvColorVision/cvColorVision.csproj 中相应的 None 项，
   设置 Link 为 cfg_files 下的相对路径，CopyToOutputDirectory 为
   PreserveNewest，Pack 为 true。只为需要这些配置的交付增加引用。
4. 若恢复主程序默认交付，同步安装器外部 ColorVision.aip 的文件、
   组件、Feature 引用及目录，并更新以下共享文件清单：
   Scripts/shared_files.json
   SDK/ColorVision.PluginKit/scripts/shared_files.json
5. 若恢复 CameraTest 独立包，同步 Scripts/package_camera_test.ps1
   的配置文件白名单。
6. 构建 Release/x64 后检查输出与归档 SHA-256，并使用目标设备验收。

删除默认配置不会移除厂商驱动、相机类型枚举或修改许可证行为。
相机服务和外部插件仍可能需要独立的设备配置，应按其交付要求处理。

维护入口：docs/04-api-reference/engine-components/cvColorVision.md
