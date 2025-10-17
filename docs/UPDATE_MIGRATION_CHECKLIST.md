# ColorVision 更新机制迁移检查清单

## 📋 文档导航

在开始之前，请先阅读以下文档：

- [ ] [执行摘要](./README_UPDATE_REDESIGN.md) - 快速了解项目概况
- [ ] [设计方案](./UPDATE_MECHANISM_REDESIGN.md) - 详细技术设计
- [ ] [实施指南](./UPDATE_IMPLEMENTATION_GUIDE.md) - 开发步骤
- [ ] [对比分析](./UPDATE_COMPARISON_ANALYSIS.md) - 新旧方案对比

---

## 🎯 决策检查清单

### 阶段 0：项目启动决策

#### 管理层决策

- [ ] 是否批准项目启动？
  - [ ] 已阅读执行摘要
  - [ ] 已阅读对比分析
  - [ ] 理解投资回报（ROI ~1.5-2年）
  - [ ] 同意 30-45 天开发时间
  - [ ] 同意双轨并行策略

- [ ] 资源分配
  - [ ] 指定项目负责人：________________
  - [ ] 分配开发人员：________________（建议 1-2 人）
  - [ ] 分配测试人员：________________
  - [ ] 预留时间：________________（建议 8 周）

- [ ] 风险接受
  - [ ] 接受短期开发成本
  - [ ] 接受可能的兼容性问题
  - [ ] 同意保留旧方案作为后备

**决策结果**：
- [ ] ✅ 批准，进入阶段 1
- [ ] ⏸️ 暂缓，原因：________________
- [ ] ❌ 拒绝，原因：________________

---

## 🏗️ 阶段 1：创建独立更新器（1-2周）

### 开发准备

- [ ] 开发环境设置
  - [ ] 安装 .NET 8.0 SDK
  - [ ] 配置 Visual Studio / Rider
  - [ ] 克隆代码仓库
  - [ ] 创建开发分支：`feature/updater-program`

### 项目创建

- [ ] 创建更新器项目
  ```bash
  dotnet new console -n ColorVision.Updater -o Tools/ColorVision.Updater
  dotnet sln add Tools/ColorVision.Updater/ColorVision.Updater.csproj
  ```

- [ ] 配置项目文件
  - [ ] 设置 TargetFramework 为 net8.0-windows
  - [ ] 添加 System.CommandLine NuGet 包
  - [ ] 添加 Newtonsoft.Json NuGet 包
  - [ ] 添加 ApplicationManifest 配置

- [ ] 创建 app.manifest
  - [ ] 请求管理员权限
  - [ ] 设置兼容性（Windows 10/11）

### 核心组件实现

#### 数据模型（Models/）

- [ ] UpdateManifest.cs
  - [ ] UpdateManifest 类
  - [ ] UpdateType 枚举
  - [ ] UpdateInfo 类
  - [ ] PathConfiguration 类
  - [ ] ExecutableConfiguration 类
  - [ ] UpdateOptions 类
  - [ ] FileOperation 类
  - [ ] FileAction 枚举

#### 日志记录（Logging/）

- [ ] UpdateLogger.cs
  - [ ] 构造函数（日志路径、最小级别）
  - [ ] Debug() 方法
  - [ ] Info() 方法
  - [ ] Warning() 方法
  - [ ] Error() 方法
  - [ ] Log() 私有方法
  - [ ] LogLevel 枚举

#### 进程管理（ProcessManagement/）

- [ ] ProcessManager.cs
  - [ ] 构造函数（接收 logger）
  - [ ] WaitForProcessExit() 异步方法
  - [ ] StartProcess() 方法
  - [ ] 超时处理逻辑
  - [ ] 异常处理

#### 文件操作（FileOperations/）

- [ ] FileOperator.cs
  - [ ] 构造函数（接收 logger）
  - [ ] CopyFile() 方法
  - [ ] DeleteFile() 方法
  - [ ] CalculateFileHash() 方法（SHA256）
  - [ ] VerifyFile() 方法
  - [ ] SafeDeleteDirectory() 方法

- [ ] BackupManager.cs
  - [ ] 构造函数（接收 logger 和 fileOperator）
  - [ ] CreateBackup() 方法
  - [ ] Rollback() 方法
  - [ ] CleanupOldBackups() 方法
  - [ ] 备份目录命名逻辑

#### 更新执行器

- [ ] UpdateExecutor.cs
  - [ ] 构造函数（接收 logger）
  - [ ] ExecuteUpdate() 主方法
  - [ ] 等待进程退出
  - [ ] 创建备份
  - [ ] 执行文件操作
  - [ ] 验证文件完整性
  - [ ] 处理失败和回滚
  - [ ] 清理临时文件
  - [ ] 重启程序

#### 主程序

- [ ] Program.cs
  - [ ] Main() 方法
  - [ ] 命令行参数定义
  - [ ] ExecuteUpdate() 方法
  - [ ] 清单读取和解析
  - [ ] 错误处理

### 单元测试

- [ ] 创建测试项目
  ```bash
  dotnet new nunit -n ColorVision.Updater.Tests -o Tools/ColorVision.Updater.Tests
  dotnet sln add Tools/ColorVision.Updater.Tests/ColorVision.Updater.Tests.csproj
  ```

- [ ] 测试覆盖
  - [ ] UpdateLogger 测试
  - [ ] FileOperator 测试
  - [ ] BackupManager 测试
  - [ ] ProcessManager 测试
  - [ ] UpdateExecutor 测试（模拟场景）
  - [ ] 测试覆盖率 > 80%

### 集成测试

- [ ] 准备测试环境
  - [ ] 创建测试应用程序
  - [ ] 创建测试更新包
  - [ ] 生成测试清单 JSON

- [ ] 测试场景
  - [ ] 正常更新流程
  - [ ] 备份创建和验证
  - [ ] 文件验证（正确哈希）
  - [ ] 文件验证失败（错误哈希）
  - [ ] 更新失败回滚
  - [ ] 权限不足处理
  - [ ] 磁盘空间不足处理

### 构建和打包

- [ ] 构建 Release 版本
  ```bash
  dotnet build -c Release
  ```

- [ ] 测试可执行文件
  - [ ] 手动运行更新器
  - [ ] 验证日志输出
  - [ ] 验证更新流程

### 阶段 1 验收

- [ ] **所有单元测试通过**
- [ ] **所有集成测试通过**
- [ ] **代码审查完成**
- [ ] **文档更新完成**
- [ ] **构建产物可用**

**阶段 1 完成日期**：________________

---

## 🔗 阶段 2：集成到主程序（3-4周）

### 准备工作

- [ ] 备份当前代码
- [ ] 创建集成分支：`feature/updater-integration`
- [ ] 合并阶段 1 的更新器代码

### 共享模型

- [ ] 创建共享库（可选）
  - [ ] 或直接复制模型类到主程序
  - [ ] 确保模型定义一致

### UpdateManager 实现

- [ ] 创建 ColorVision/Update/UpdateManager.cs
  - [ ] 单例模式实现
  - [ ] Config 属性
  - [ ] PrepareApplicationUpdate() 方法
  - [ ] PreparePluginUpdate() 方法
  - [ ] ExecuteUpdate() 方法
  - [ ] GetUpdaterExecutablePath() 方法
  - [ ] ValidateUpdaterExists() 方法
  - [ ] EnsureUpdaterExists() 方法
  - [ ] ExtractUpdaterFromResources() 方法

### UpdateManagerConfig 实现

- [ ] 创建 ColorVision/Update/UpdateManagerConfig.cs
  - [ ] 实现 IConfig 接口
  - [ ] UseNewUpdateMechanism 属性
  - [ ] UpdaterPath 属性
  - [ ] EnableBackup 属性
  - [ ] BackupRetentionDays 属性
  - [ ] TempUpdateDirectory 属性
  - [ ] 保存/加载逻辑

### 嵌入更新器资源

- [ ] 将 ColorVision.Updater.exe 作为嵌入资源
  - [ ] 添加到项目资源
  - [ ] 设置 Build Action 为 Embedded Resource
  - [ ] 实现提取逻辑

### 改造 AutoUpdater

- [ ] 修改 ColorVision/Update/AutoUpdater.cs
  - [ ] 重命名原方法为 *_Old
  - [ ] 实现新的 RestartIsIncrementApplication()
  - [ ] 实现新的 RestartApplication()
  - [ ] 添加配置检查逻辑
  - [ ] 保留旧方法作为后备

### 改造 PluginUpdater

- [ ] 修改 UI/ColorVision.UI/Plugins/PluginUpdater.cs
  - [ ] 重命名原方法为 *_Old
  - [ ] 实现新的 UpdatePlugin()
  - [ ] 实现新的 DeletePlugin()（如需要）
  - [ ] 添加配置检查逻辑
  - [ ] 保留旧方法作为后备

### 配置界面

- [ ] 添加更新设置界面
  - [ ] 新旧方案切换开关
  - [ ] 备份设置选项
  - [ ] 更新器路径配置
  - [ ] 临时目录配置
  - [ ] 备份保留天数

### 测试

- [ ] 单元测试
  - [ ] UpdateManager 测试
  - [ ] UpdateManagerConfig 测试
  - [ ] 改造后的 AutoUpdater 测试

- [ ] 集成测试
  - [ ] 应用程序完整更新（新方案）
  - [ ] 应用程序增量更新（新方案）
  - [ ] 插件更新（新方案）
  - [ ] 批量插件更新（新方案）
  - [ ] 新旧方案切换测试

### 阶段 2 验收

- [ ] **所有测试通过**
- [ ] **新旧方案都可用**
- [ ] **配置界面可用**
- [ ] **代码审查完成**
- [ ] **文档更新完成**

**阶段 2 完成日期**：________________

---

## 🧪 阶段 3：双轨并行测试（5-6周）

### 测试环境准备

- [ ] 搭建测试环境
  - [ ] Windows 10 测试机
  - [ ] Windows 11 测试机
  - [ ] Program Files 安装测试
  - [ ] 自定义路径安装测试

### 功能测试

#### 应用程序更新

- [ ] 完整更新测试
  - [ ] 小版本更新（<10MB）
  - [ ] 中版本更新（10-50MB）
  - [ ] 大版本更新（>100MB）
  - [ ] 跨多个版本更新

- [ ] 增量更新测试
  - [ ] 小增量（几个文件）
  - [ ] 大增量（多个文件）
  - [ ] 混合更新（新增+修改+删除）

#### 插件更新

- [ ] 单个插件更新
- [ ] 多个插件批量更新
- [ ] 插件卸载测试

### 边界测试

- [ ] 异常场景
  - [ ] 更新包损坏
  - [ ] 磁盘空间不足
  - [ ] 权限不足
  - [ ] 文件被占用
  - [ ] 网络中断（下载时）
  - [ ] 主程序未能退出
  - [ ] 更新器崩溃

- [ ] 回滚测试
  - [ ] 文件复制失败回滚
  - [ ] 文件验证失败回滚
  - [ ] 手动回滚工具测试

### 性能测试

- [ ] 测试更新时间
  - [ ] 记录小更新耗时
  - [ ] 记录中更新耗时
  - [ ] 记录大更新耗时
  - [ ] 与旧方案对比

- [ ] 测试资源占用
  - [ ] CPU 占用率
  - [ ] 内存占用
  - [ ] 磁盘 I/O

### 兼容性测试

- [ ] Windows 版本
  - [ ] Windows 10 1809+
  - [ ] Windows 11 21H2+
  - [ ] Windows Server 2019+

- [ ] 安装路径
  - [ ] C:\Program Files\
  - [ ] C:\Program Files (x86)\
  - [ ] 自定义路径（D:\、E:\ 等）
  - [ ] 中文路径
  - [ ] 长路径

### 用户体验测试

- [ ] 更新流程顺畅性
- [ ] 错误提示清晰度
- [ ] 日志可读性
- [ ] 配置界面易用性

### 旧方案验证

- [ ] 确保旧 BAT 方式仍然可用
- [ ] 测试新旧方案切换
- [ ] 验证配置保存和恢复

### Bug 跟踪

建立 Bug 跟踪表：

| Bug ID | 严重程度 | 描述 | 状态 | 修复日期 |
|--------|---------|------|------|---------|
| | | | | |

### 阶段 3 验收

- [ ] **所有功能测试通过**
- [ ] **所有边界测试通过**
- [ ] **性能指标达标**
- [ ] **兼容性测试通过**
- [ ] **无严重 Bug**
- [ ] **测试报告完成**

**阶段 3 完成日期**：________________

---

## 🚀 阶段 4：完全迁移（7周）

### 灰度发布计划

- [ ] 第 1 周：10% 用户
  - [ ] 修改默认配置：UseNewUpdateMechanism = true（10% 用户）
  - [ ] 监控更新日志
  - [ ] 收集用户反馈

- [ ] 第 2 周：50% 用户
  - [ ] 如无严重问题，扩大到 50%
  - [ ] 继续监控
  - [ ] 继续收集反馈

- [ ] 第 3-4 周：100% 用户
  - [ ] 全量切换
  - [ ] 密切监控
  - [ ] 快速响应问题

### 监控指标

- [ ] 更新成功率
  - [ ] 目标：> 99%
  - [ ] 当前：______%

- [ ] 更新平均时间
  - [ ] 目标：< 旧方案 50%
  - [ ] 当前：______秒

- [ ] 错误率
  - [ ] 目标：< 1%
  - [ ] 当前：______%

### 用户反馈

建立反馈跟踪表：

| 日期 | 用户 | 反馈 | 类型 | 处理状态 |
|------|-----|------|------|---------|
| | | | Bug/建议 | |

### 阶段 4 验收

- [ ] **灰度发布完成**
- [ ] **100% 用户使用新方案**
- [ ] **更新成功率 > 99%**
- [ ] **无严重用户投诉**
- [ ] **稳定运行 2 周**

**阶段 4 完成日期**：________________

---

## 🧹 阶段 5：清理优化（8周）

### 代码清理

- [ ] 标记旧方法为 Obsolete
  ```csharp
  [Obsolete("使用新的 UpdateManager，将在下个版本移除")]
  private static void RestartIsIncrementApplication_Old(string downloadPath)
  ```

- [ ] 等待 1-2 个版本后删除
  - [ ] 删除 *_Old 方法
  - [ ] 删除 GenerateBatchFile 方法
  - [ ] 删除 BAT 脚本生成相关代码
  - [ ] 删除 UseNewUpdateMechanism 配置项

### 代码优化

- [ ] 性能优化
  - [ ] 分析性能瓶颈
  - [ ] 优化文件复制速度
  - [ ] 优化内存使用

- [ ] 代码重构
  - [ ] 简化复杂方法
  - [ ] 提取公共逻辑
  - [ ] 改进命名

### 文档更新

- [ ] 更新用户手册
- [ ] 更新开发者文档
- [ ] 更新 API 文档
- [ ] 更新变更日志

### 发布正式版本

- [ ] 创建发布分支
- [ ] 更新版本号
- [ ] 生成发布说明
- [ ] 打包发布

### 阶段 5 验收

- [ ] **旧代码完全移除**
- [ ] **代码优化完成**
- [ ] **文档更新完成**
- [ ] **正式版本发布**

**阶段 5 完成日期**：________________

---

## 📊 项目总结

### 项目统计

| 指标 | 计划 | 实际 | 差异 |
|------|------|------|------|
| 总耗时（天） | 30-45 | ______ | ______ |
| 代码行数 | ~1050 | ______ | ______ |
| 测试覆盖率 | > 80% | ______ | ______ |
| Bug 数量 | < 10 | ______ | ______ |
| 更新成功率 | > 99% | ______ | ______ |

### 经验教训

**成功经验**：
- 
- 

**改进建议**：
- 
- 

### 后续计划

- [ ] 添加差分更新支持
- [ ] 添加自动静默更新
- [ ] 跨平台支持（Linux、macOS）
- [ ] 更新服务器优化

---

## ✅ 最终验收

### 项目交付物

- [ ] ColorVision.Updater.exe（独立更新器）
- [ ] UpdateManager（集成到主程序）
- [ ] 单元测试项目
- [ ] 测试报告
- [ ] 用户文档
- [ ] 开发者文档

### 项目目标达成

- [ ] ✅ 替换 BAT 脚本更新方案
- [ ] ✅ 提升更新可靠性
- [ ] ✅ 改善用户体验
- [ ] ✅ 降低维护成本
- [ ] ✅ 支持未来扩展

### 项目签收

- [ ] 项目负责人签字：________________ 日期：________
- [ ] 技术负责人签字：________________ 日期：________
- [ ] 测试负责人签字：________________ 日期：________

---

**项目完成日期**：________________  
**文档版本**：1.0  
**维护者**：ColorVision 开发团队
