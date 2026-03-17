# CHANGELOG

## [1.2.0.0] 2026.03.17

1. **ProcessGroup（流程组）功能：**
   - 新增ProcessGroup概念，支持将ProcessMeta组织为不同的组
   - 用户可切换不同的组来执行不同的测试方案（如不同产品）
   - 支持组的添加、删除、重命名、复制操作
   - 旧版ProcessMetas.json自动迁移为Default组，完全向后兼容

2. **一键执行功能：**
   - 新增"一键执行"按钮，可一键执行当前组所有启用的流程并解析结果
   - 执行过程中自动管理异步等待、错误处理和超时
   - 支持失败跳过（基于AllowTestFailures配置）

3. **步间通信指令（InterStepAction）：**
   - 支持在两个流程之间插入通信指令，触发外部设备切图后再执行下一步
   - 支持Socket通信、串口通信、SwitchPG协议和延时四种类型
   - 每个ProcessMeta可独立配置步间指令
   - 在ProcessManagerWindow中可通过"编辑"按钮配置

4. **Socket协议扩展：**
   - 新增SwitchGroup事件，支持外部系统切换流程组
   - 新增RunAll事件，支持外部系统触发一键执行
   - 现有协议（ProjectARVRInit/SwitchPGCompleted/ProjectARVRResult）保持不变

5. **UI增强：**
   - ARVRWindow状态栏增加组选择器ComboBox
   - ProcessManagerWindow增加组管理工具栏
   - ProcessManagerWindow ListView增加步间指令编辑列

## [1.1.2.14] 2025.12.27

1.修复一些BUG

2.增加对MTF048的支持

## [1.1.1.12] 2025.12.11

1.允许保存时按照日期存储

## [1.1.1.6] 2025.12.10

1.支持link和保存图像

## [1.0.4.7] 2025.12.09

1. 文档更新和优化：
   - README.md全面更新，增加详细的功能说明和架构设计
   - 新增版本徽章和表格化功能列表
   - 添加Process目录结构说明
   - 完善ProcessConfig可配置Key文档
   - 增加IProcess接口和ProcessBase基类说明

2. 新增ROADMAP.md开发路线图文档

## [1.0.4.4] 2025.12.04

1. 新增MTFHV058Process模块，支持058产品的MTF测试

2. MTFHVProcess和MTFHV058Process优化：
   - 将解析Key从硬编码移动到ProcessConfig中
   - 支持用户自定义配置解析使用的变量名
   - 默认值保持不变，确保向后兼容

3. ProcessManager配置独立性修复：
   - 每个ProcessMeta现在拥有独立的Process实例和Config配置
   - 修改一个流程的Config不再影响其他使用相同Process类型的流程
   - 新建、更新和加载流程时都会创建独立的Process实例

## [1.0.4.1] 2025.12.01

1. 优化选项，统一结构

## [1.0.3.4] 2025.11.17

1. 增加ProcessMeta的IsEnabled属性，支持选择性执行测试步骤

2. 优化流程管理：
   - ProcessManagerWindow新增"是否启用"列，支持通过复选框控制步骤启用/禁用
   - 执行逻辑自动跳过禁用的步骤
   - 完成判断仅基于启用的步骤
   - 初始化时动态查找第一个启用的步骤

3. 持久化支持：IsEnabled状态自动保存到ProcessMetas.json

## [1.0.0.15] 2025.10.21

1. ARVR优化项目

2. 增加ProcessManagerWindow，拆分Process项目

3. 增加Red,Green,Blue的解析