# CHANGELOG

## [1.4.3.8] 2025.04.14

### 新增
- 内置服务管理器 (`ServiceManagerWindow`)：支持查看服务状态、一键启停、打开目录与配置、MySQL/MQTT 状态监控。
- 服务安装/更新窗口 (`ServiceInstallWindow`)：支持在线检测版本、下载完整安装包与增量更新包、一键安装包解析、数据库备份/恢复、服务文件夹备份/恢复。
- 增量更新包支持：识别 `X.X.X.X` 根目录格式，仅覆盖变更文件并跳过 `cfg/` 保留现有配置。
- MySQL 运维功能：ZIP 安装、启动/停止/卸载、数据库备份/恢复、root 密码设置与强制重置、业务用户创建与删除。
- MQTT 安装支持：下载并执行 mosquitto 安装程序。
- 配置同步：将当前系统 MySQL / MQTT / RC 配置一键写入所有服务的 `cfg/*.config`。
- 旧版 `App.config` 兼容：读取并同步 CVWinSMS 遗留配置。

### 改进
- 重构原有 `UpdateService` 逻辑，合并到 `ServiceInstallViewModel` 统一安装/更新流水线。
- 统一使用 `ServiceManagerConfig` 管理路径、端口、更新地址、下载目录。
- 优化服务安装流程：自动复制 `CommonDll`、清理 `pack` 目录、支持注册表自动检测 `BaseLocation`。
- 规范插件代码结构，按功能拆分为 `ServiceManager/`、`CVWinSMS/`、`Menus/`、`Tools/` 目录。

### 修复
- 修复服务路径解析在嵌套 `CVWindowsService` 目录下的兼容性问题。
- 修复备份/恢复时日志目录被意外打包的问题。
- 修复 MySQL 服务卸载时进程残留导致的重新安装失败问题。

## [1.2.3.4] 2025.05.23

1. 规范插件的写法。
