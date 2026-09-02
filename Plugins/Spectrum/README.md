# Spectrum 光谱测量插件

Spectrum 是 ColorVision 的光谱仪测量插件，可从 **工具 → 光谱仪软件** 打开，也是可独立启动的 Windows WPF 程序；独立入口仍需要 ColorVision 库与原生驱动。

## 运行与设备前提

- 当前工程目标与依赖以 `Spectrum.csproj`、`Plugins/Directory.Build.props` 为准，使用匹配的 Windows/x64 运行环境、原生 DLL、设备驱动、许可证和标定文件。
- 插件身份及最低宿主要求见 `manifest.json`；专用发布脚本读取编译后 `Spectrum.exe` 的 `FileVersion` 并同步 manifest，版本设置在 `Spectrum.csproj`。
- 连接成功不等于标定就绪或可测量。连接、校零、快门、滤光轮、SMU 和测量会影响真实设备，必须先取得相应现场授权。
- 主测量 `CM_*` 与 `DirectSpectrometer` 的 `SA_*` 诊断连接不能同时使用；取消令牌不能强制中断已经进入的原生调用。窗口关闭也不保证在固定秒数内完成或所有设备已安全释放。
- Socket/调度入口复用设备 Manager，不以 Spectrum 窗口是否打开判定可执行；仍要求进程存活、入口已启用、设备及标定状态满足操作条件。

## 权威知识入口

- [Spectrum 测量、标定、结果与交付](../../docs/04-api-reference/plugins/standard-plugins/spectrum.md)：连接、单次测量与导出步骤，以及标定、结果事务、窗口生命周期和双通道发布。
- [Spectrum Socket 业务指令](../../docs/04-api-reference/plugins/standard-plugins/spectrum-socket.md)：五个指令、字段类型、设备锁及合作式取消。

正式 Spectrum 发布使用仓库的专用 `Scripts/Spectrum.bat`，会更新独立 ZIP 与 ColorVision `.cvxp` 两个远端源，必须另获发布授权；普通构建、阅读协议或本地测试不授权上传。

上述相对文档链接仅在匹配版本的完整源码仓库中有效。若当前交付物未包含 `docs/`，应回到同版本仓库读取完整契约；不能仅凭程序可启动认定设备操作或交付已经验证。
