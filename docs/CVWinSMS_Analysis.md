# CVWinSMS 旧软件功能分析

## 文件位置
`C:\Users\17917\Desktop\CVWinSMS\CVWinSMS\MainForm.cs`

## 核心功能模块

### 1. 服务管理 (CVWinService)
- **TPAWindowsService** / **TPAWindowsService32** - TPA服务
- **CVMainService_x64** / **CVMainService_dev** - 主服务
- **RegistrationCenterService** / **RegistrationCenterService_arch** - 注册中心服务

### 2. MySQL 管理 (CVMysqlControl)
- MySQL 服务状态监控
- ZIP 包安装 MySQL (`mysql-5.7.37-winx64.zip`)
- 启动/停止/卸载服务
- 数据库备份/还原
- SQL 脚本执行
- root 密码设置/重置
- 业务用户创建/更新
- 连接字符串配置

### 3. MQTT 管理 (CVMQTTUIControl)
- MQTT 服务状态监控
- mosquitto 安装 (`mosquitto-2.0.18-install-windows-x64.exe`)
- 启动/停止服务
- 主题订阅管理
- 设备初始化

### 4. Restful API 管理 (CVRestfulAPIControl)
- API 服务配置
- 端口配置

### 5. 一键操作
- **一键安装** (`button_install_all`)
  - 解压 FullPackage.zip
  - 安装 mosquitto
  - 解压 mysql
  - 安装并启动所有服务
- **一键启动** (`button_start_all`)
- **一键停止** (`button_stop_all`)

### 6. 更新功能
- **在线下载** (`button_online_update`) - 打开浏览器下载页面
- **增量升级** (`button_update_increment`) - 解压更新包，复制文件，重启服务
- **全新安装** - 卸载服务、备份数据库、重新安装、执行SQL

### 7. 日志功能
- 实时日志输出 (`richTextBox_output`)
- 日志收集打包
- 日志清理

### 8. 归档服务 (ArchivedService)
- 数据库归档配置
- 定时归档任务

## 关键方法

### 安装流程
```csharp
OneKeyInstall(string zipFile, string installPath, BackgroundWorker backgroundWorker)
```
1. 解压 pack 目录
2. 安装 mosquitto
3. 解压 mysql
4. 启动 MQTT
5. 安装 CVWindowsService
6. 执行初始化 SQL

### 更新流程
```csharp
DoUpdateAndReinstall(UnPackageArgs args)
```
1. 停止服务
2. 解压更新包
3. 复制文件
4. 更新数据库
5. 重启服务

### 增量升级
```csharp
backgroundWorker_update_inc_DoWork
```
1. 停止服务
2. 解压增量包
3. 执行 SQL 更新
4. 重启服务

## 配置存储
- SysConfig 类存储配置
- 配置文件路径可自定义
- 支持从旧版 App.config 迁移

## 数据库操作
- 使用 MySqlConnector
- 支持数据库备份/还原
- SQL 脚本执行
- 连接字符串动态更新
