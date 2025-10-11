# ColorVision.Engine 重构实施检查清单

## 📋 使用说明

本文档提供 ColorVision.Engine 重构项目的实施检查清单，帮助团队跟踪进度。

**相关文档**:
- [完整技术方案](./ColorVision.Engine-Refactoring-Plan.md)
- [执行摘要](./ColorVision.Engine-Refactoring-Summary.md)

---

## 阶段 1: 准备阶段 (1-2周)

### 代码分析和评估
- [ ] 使用 NDepend 或 SonarQube 分析代码依赖
- [ ] 绘制当前依赖关系图
- [ ] 识别循环依赖点（记录在文档中）
- [ ] 评估拆分风险等级（高/中/低）
- [ ] 制定详细的文件迁移清单（Excel或Markdown）

### 基础设施搭建
- [ ] 创建新的解决方案文件 `ColorVision.Engine.Modular.sln`
- [ ] 配置 CI/CD 流程
  - [ ] 自动构建
  - [ ] 自动测试
  - [ ] 代码质量检查
- [ ] 搭建单元测试框架（xUnit 或 NUnit）
- [ ] 配置代码覆盖率工具（Coverlet）
- [ ] 搭建 API 文档生成系统（DocFX）

### 接口设计
- [ ] 设计 IEngineService 接口
- [ ] 设计 IDeviceService 接口
- [ ] 设计 ITemplateEngine 接口
- [ ] 设计 IAlgorithmService 接口
- [ ] 设计 IEventBus 接口
- [ ] 设计 IServiceBus 接口
- [ ] 进行接口设计评审会议
- [ ] 更新接口设计文档

### 交付物检查
- [ ] ✅ 依赖关系分析报告
- [ ] ✅ 核心接口设计文档
- [ ] ✅ 新项目结构已创建
- [ ] ✅ 文件迁移计划 Excel/Markdown
- [ ] ✅ 团队评审通过

---

## 阶段 2: Core 模块开发 (2-3周)

### 项目创建
- [ ] 创建 `ColorVision.Engine.Core` 类库项目
- [ ] 配置 .NET 版本和依赖
- [ ] 设置项目属性（Nullable、LangVersion等）

### 目录结构创建
- [ ] 创建 `Abstractions/` 目录
- [ ] 创建 `Services/` 目录
- [ ] 创建 `DependencyInjection/` 目录
- [ ] 创建 `Configuration/` 目录
- [ ] 创建 `Models/` 目录
- [ ] 创建 `Events/` 目录

### 核心接口实现
- [ ] 实现 `IEngineService.cs`
- [ ] 实现 `IDeviceService.cs`
- [ ] 实现 `ITemplateEngine.cs`
- [ ] 实现 `IAlgorithmService.cs`
- [ ] 实现 `IEventBus.cs`
- [ ] 实现 `IServiceBus.cs`

### 核心服务实现
- [ ] 实现 `ServiceBus.cs`
- [ ] 实现 `EventBus.cs`
- [ ] 实现 `ServiceRegistry.cs`
- [ ] 实现 `ConfigurationManager.cs`

### 依赖注入
- [ ] 实现 `ServiceCollectionExtensions.cs`
  - [ ] AddEngineCore() 方法
  - [ ] 服务注册逻辑
- [ ] 实现自定义 ServiceProvider（如需要）

### 单元测试
- [ ] ServiceBus 单元测试（≥80% 覆盖率）
- [ ] EventBus 单元测试（≥80% 覆盖率）
- [ ] ConfigurationManager 单元测试
- [ ] 依赖注入测试

### 文档
- [ ] Core 模块 API 参考文档
- [ ] 使用示例代码
- [ ] 架构说明文档

### 交付物检查
- [ ] ✅ ColorVision.Engine.Core.dll 编译成功
- [ ] ✅ 单元测试通过率 100%
- [ ] ✅ 代码覆盖率 ≥ 80%
- [ ] ✅ API 文档已生成
- [ ] ✅ 代码审查通过

---

## 阶段 3: Infrastructure + Communication (2周)

### ColorVision.Engine.Infrastructure
- [ ] 创建项目和目录结构
- [ ] 迁移文件列表（记录源和目标路径）:
  - [ ] Utilities/ → Infrastructure/Utilities/
  - [ ] Reports/ → Infrastructure/Reports/
  - [ ] Media/ → Infrastructure/Media/
  - [ ] ToolPlugins/ → Infrastructure/ToolPlugins/
- [ ] 更新命名空间
- [ ] 修复编译错误
- [ ] 实现 AddEngineInfrastructure() 扩展方法
- [ ] 单元测试（≥80% 覆盖率）

### ColorVision.Engine.Communication
- [ ] 创建项目和目录结构
- [ ] 迁移文件列表:
  - [ ] MQTT/ → Communication/MQTT/
  - [ ] Messages/ → Communication/Messages/
- [ ] 创建通信抽象接口
  - [ ] ICommunicationService.cs
  - [ ] IMQTTService.cs
- [ ] 实现 MQTTService
- [ ] 实现消息处理器
- [ ] 实现 AddEngineCommunication() 扩展方法
- [ ] 单元测试（使用 Mock MQTT Broker）

### 集成测试
- [ ] Core + Infrastructure 集成测试
- [ ] Core + Communication 集成测试

### 交付物检查
- [ ] ✅ ColorVision.Engine.Infrastructure.dll
- [ ] ✅ ColorVision.Engine.Communication.dll
- [ ] ✅ 所有测试通过
- [ ] ✅ 文档已更新

---

## 阶段 4: Data 模块 (2周)

### 项目创建
- [ ] 创建 `ColorVision.Engine.Data` 项目
- [ ] 添加 SqlSugarCore NuGet 包
- [ ] 添加必要的数据库驱动

### 目录结构
- [ ] 创建 `Repositories/` 目录
- [ ] 创建 `Entities/` 目录
- [ ] 创建 `DbContexts/` 目录
- [ ] 创建 `Migrations/` 目录

### 数据层迁移
- [ ] 迁移 Dao/ 目录文件
- [ ] 迁移实体模型
- [ ] 实现 IRepository\<T\> 接口
- [ ] 实现仓储类
- [ ] 实现 EngineDbContext
- [ ] 实现批量数据管理器

### 数据迁移
- [ ] 创建数据库迁移脚本
- [ ] 测试数据库向后兼容性
- [ ] 编写数据迁移指南

### 优化
- [ ] 优化数据库查询（使用 EXPLAIN）
- [ ] 实现查询缓存
- [ ] 实现批量操作优化

### 测试
- [ ] 仓储单元测试（使用内存数据库）
- [ ] 数据库集成测试
- [ ] 性能测试（记录基准数据）

### 交付物检查
- [ ] ✅ ColorVision.Engine.Data.dll
- [ ] ✅ 数据库迁移脚本
- [ ] ✅ 测试通过
- [ ] ✅ 性能基准文档

---

## 阶段 5: Devices 模块 (3-4周)

### 项目创建
- [ ] 创建 `ColorVision.Engine.Devices` 项目

### 设备抽象层
- [ ] 创建 `Core/` 目录
- [ ] 实现 `IDevice.cs` 接口
- [ ] 实现 `DeviceBase.cs` 抽象类
- [ ] 实现 `DeviceManager.cs`
- [ ] 实现 `DeviceServiceBase.cs`

### 设备迁移（按优先级）
**Week 1:**
- [ ] 迁移 Camera 设备
  - [ ] CameraService.cs
  - [ ] CameraConfig.cs
  - [ ] Templates/
  - [ ] Views/
  - [ ] 单元测试

**Week 2:**
- [ ] 迁移 Spectrum 设备
- [ ] 迁移 SMU 设备
- [ ] 单元测试

**Week 3:**
- [ ] 迁移 Motor 设备
- [ ] 迁移 Sensor 设备
- [ ] 迁移 PG 设备
- [ ] 单元测试

**Week 4:**
- [ ] 迁移 ThirdParty 设备集成
- [ ] 迁移其他设备
- [ ] 集成测试

### 依赖注入
- [ ] 实现 AddEngineDevices() 扩展方法
- [ ] 实现设备注册机制

### 测试
- [ ] 各设备单元测试
- [ ] 设备管理器测试
- [ ] 硬件模拟测试
- [ ] 集成测试

### 交付物检查
- [ ] ✅ ColorVision.Engine.Devices.dll
- [ ] ✅ 所有设备测试通过
- [ ] ✅ 设备接口文档
- [ ] ✅ 使用示例

---

## 阶段 6: Templates + Algorithms (4-5周)

### ColorVision.Engine.Templates
**Week 1: 基础架构**
- [ ] 创建项目
- [ ] 创建 Core/ 目录
- [ ] 实现 `ITemplate.cs`
- [ ] 实现 `TemplateBase.cs`
- [ ] 实现 `TemplateManager.cs`

**Week 2: POI 模板**
- [ ] 迁移 POI/AlgorithmImp
- [ ] 迁移 POI/BuildPoi
- [ ] 迁移 POI/POIFilters
- [ ] 迁移 POI/POIGenCali
- [ ] 迁移 POI/POIOutput
- [ ] 迁移 POI/POIRevise
- [ ] 单元测试

**Week 3: ARVR 模板**
- [ ] 迁移 ARVR/MTF
- [ ] 迁移 ARVR/SFR
- [ ] 迁移 ARVR/FOV
- [ ] 迁移 ARVR/Distortion
- [ ] 迁移 ARVR/Ghost
- [ ] 单元测试

**Week 4: 其他模板**
- [ ] 迁移 ImageProcessing 相关模板
  - [ ] LEDStripDetection
  - [ ] LedCheck
  - [ ] ImageCropping
- [ ] 迁移 Analysis 相关模板
  - [ ] JND
  - [ ] Compliance
  - [ ] Matching
- [ ] 单元测试

**Week 5: 集成和优化**
- [ ] 模板系统集成测试
- [ ] 算法精度验证
- [ ] 性能优化

### ColorVision.Engine.Algorithms
- [ ] 创建项目
- [ ] 实现 AlgorithmService
- [ ] 实现 AlgorithmNodeBase
- [ ] 迁移 ThirdPartyAlgorithmManager
- [ ] 实现 AlgorithmExecutor
- [ ] 单元测试

### 依赖注入
- [ ] 实现 AddEngineTemplates() 扩展方法
- [ ] 实现 AddEngineAlgorithms() 扩展方法

### 交付物检查
- [ ] ✅ ColorVision.Engine.Templates.dll
- [ ] ✅ ColorVision.Engine.Algorithms.dll
- [ ] ✅ 算法精度测试报告
- [ ] ✅ 性能测试报告
- [ ] ✅ 文档更新

---

## 阶段 7: Flow 模块 (2-3周)

### 项目创建
- [ ] 创建 `ColorVision.Engine.Flow` 项目

### 流程引擎迁移
- [ ] 迁移 FlowEngineManager.cs
- [ ] 迁移 TemplateFlow.cs
- [ ] 迁移 FlowParam 相关类
- [ ] 迁移流程节点定义
- [ ] 迁移流程模板管理

### 流程执行器
- [ ] 实现 FlowExecutor
- [ ] 实现流程状态管理
- [ ] 实现流程监控

### 依赖注入
- [ ] 实现 AddEngineFlow() 扩展方法

### 测试
- [ ] 流程引擎单元测试
- [ ] 流程执行测试
- [ ] 复杂流程场景测试
- [ ] 性能测试

### 交付物检查
- [ ] ✅ ColorVision.Engine.Flow.dll
- [ ] ✅ 流程引擎测试通过
- [ ] ✅ 文档更新

---

## 阶段 8: PhysicalDevices (1周)

### 项目创建
- [ ] 创建 `ColorVision.Engine.PhysicalDevices` 项目

### 迁移
- [ ] 迁移 PhyCameras/
  - [ ] PhyCameraManager.cs
  - [ ] 相关配置和视图
- [ ] 迁移 PhySpectrums/
  - [ ] PhySpectrumManager.cs
  - [ ] 相关配置

### 依赖注入
- [ ] 实现 AddEnginePhysicalDevices() 扩展方法

### 测试
- [ ] 物理设备管理测试

### 交付物检查
- [ ] ✅ ColorVision.Engine.PhysicalDevices.dll
- [ ] ✅ 测试通过

---

## 阶段 9: 集成和测试 (2-3周)

### 系统集成
- [ ] 创建主启动项目（或更新现有）
- [ ] 配置所有模块的依赖注入
- [ ] 解决模块间集成问题
- [ ] 更新 ColorVision.UI 以使用新架构

### 配置文件
- [ ] 更新 appsettings.json
- [ ] 配置模块加载顺序
- [ ] 配置日志级别

### 全面测试
**单元测试**
- [ ] 确保所有模块单元测试通过
- [ ] 代码覆盖率 ≥ 80%

**集成测试**
- [ ] Core + Infrastructure 集成
- [ ] Core + Communication 集成
- [ ] Core + Data 集成
- [ ] Core + Devices 集成
- [ ] Core + Templates 集成
- [ ] Core + Flow 集成
- [ ] 全模块集成测试

**系统测试**
- [ ] 功能测试（所有主要功能）
- [ ] 回归测试（确保无功能丢失）
- [ ] 用户验收测试

**性能测试**
- [ ] 启动时间测试
- [ ] 内存占用测试
- [ ] 模块加载时间测试
- [ ] API 响应时间测试
- [ ] 压力测试

### 文档完善
- [ ] 架构文档更新
- [ ] API 文档完善
- [ ] 编写迁移指南
- [ ] 编写故障排查指南
- [ ] 编写开发者指南
- [ ] 更新 README

### 交付物检查
- [ ] ✅ 所有模块编译成功
- [ ] ✅ 所有测试通过
- [ ] ✅ 性能指标达标
- [ ] ✅ 文档完整

---

## 阶段 10: 部署和优化 (1-2周)

### 部署准备
- [ ] 创建 NuGet 包（如需要）
- [ ] 配置版本号
- [ ] 编写发布说明
- [ ] 准备安装包

### 性能优化
- [ ] 启动时间优化
  - [ ] 延迟加载非关键模块
  - [ ] 并行初始化
- [ ] 内存占用优化
  - [ ] 检查内存泄漏
  - [ ] 优化资源使用
- [ ] 加载性能优化
  - [ ] 优化依赖加载
  - [ ] 缓存优化

### 监控和日志
- [ ] 配置应用程序监控
- [ ] 配置性能计数器
- [ ] 配置日志收集

### 文档
- [ ] 编写运维手册
- [ ] 编写部署指南
- [ ] 编写版本升级指南

### 生产验证
- [ ] 预生产环境测试
- [ ] 生产环境部署
- [ ] 生产验证测试

### 交付物检查
- [ ] ✅ 生产环境部署成功
- [ ] ✅ 性能指标达标
- [ ] ✅ 监控和日志正常
- [ ] ✅ 运维文档完整

---

## 质量门禁

### 每个模块必须满足
- [ ] 编译无警告
- [ ] 单元测试覆盖率 ≥ 80%
- [ ] 所有单元测试通过
- [ ] 代码审查通过
- [ ] 静态分析无严重问题
- [ ] 文档已更新

### 每个阶段必须满足
- [ ] 所有模块质量门禁通过
- [ ] 集成测试通过
- [ ] 性能测试通过
- [ ] 团队评审通过

### 最终发布必须满足
- [ ] 所有功能测试通过
- [ ] 性能指标达标:
  - [ ] 启动时间 ≤ 5秒
  - [ ] 模块加载 ≤ 500ms
  - [ ] 内存占用降低 ≥ 30%
- [ ] 代码质量指标达标:
  - [ ] 覆盖率 ≥ 80%
  - [ ] 圈复杂度 ≤ 10
  - [ ] 重复率 ≤ 5%
- [ ] 文档完整性 100%

---

## 进度跟踪

### 当前阶段
- **阶段**: [填写]
- **开始日期**: [填写]
- **计划完成日期**: [填写]
- **实际完成日期**: [填写]
- **进度**: [0-100]%

### 风险和问题
| ID | 风险/问题 | 严重程度 | 状态 | 负责人 | 解决方案 |
|----|----------|---------|------|--------|---------|
| 1  |          |         |      |        |         |

### 变更记录
| 日期 | 变更内容 | 影响 | 批准人 |
|------|---------|------|--------|
|      |         |      |        |

---

**文档版本**: v1.0  
**最后更新**: 2025-01-08  
**负责人**: [待定]
