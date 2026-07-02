# CHANGELOG

## [1.1.7.23] 2026.07.02

1. 修复流程完成后同一个测试结果可能被重复写入并在结果列表显示两行的问题。
2. 优化 `SwitchPGCompleted` 与 Flow 完成事件的竞态保护，避免客户侧过快或重复回包导致同一流程重复触发。
3. 修复 DemuraTool 每次执行都覆盖复制工具 DLL，导致 DLL 被 ColorVision.exe 加载后再次复制时报文件占用的问题。
4. 失败/超时结果也会按批次回填拍照图像，便于计算失败时查看原始图。
5. 失败结果缺失 Model 时自动回填当前 Flow 名称，避免结果列表出现空 Model。

## [1.1.7.21] 2026.07.02

1. 接入 ImageView 图片组能力：同一批次存在多张拍摄图时，可在结果图像左下角左右切换并显示当前序号。

## [1.1.7.20] 2026.07.01

1. Demura GenText 中 W128/G255 CSV 也改为仅显示文件名，并支持双击打开文件位置。
2. Demura 结果恢复显示本批次拍摄图像，CSV/bin 不再占用主图位置。
3. 增加烧录状态说明：当前仅生成待烧录 bin，烧录接口未接入。

## [1.1.7.19] 2026.07.01

1. 优化 Demura GenText：仅显示 Dynamic/Merged 两个 bin 文件名，并支持在结果文本中双击文件名打开文件位置。

## [1.1.7.18] 2026.07.01

1. 简化 DemuraTool 运行目录：不再按批次复制多份工具，直接在用户目录 DemuraTool 下覆盖写入 G128/G255 和 bin 文件。

## [1.1.7.17] 2026.07.01

1. 修复 Demura 结果主文件指向 DemuraMerged.bin 后，结果界面按图片加载并弹出“不支持的图片格式 .bin”的问题。

## [1.1.7.16] 2026.07.01

1. 新增 Demura 解析流程：按批次读取 ImageConvert(type=93) 结果，自动识别 W128/W255 CSV。
2. 自动准备现场工具目录，将 W128/W255 复制为 G128.csv/G255.csv。
3. 打包 DemuraTool_x64 及 DLL，直接调用 DemuraSaveBin.dll 生成 DemuraStatic.bin、DemuraDynamic.bin、DemuraMerged.bin。

## [1.1.7.7] 2026.06.04

1. 修复流程组切换后，ARVRWindow 执行链仍可能使用窗口创建时旧流程组的问题。
2. 修复独立启动 ProjectARVRPro 时 socket 入口可能创建第二个 ARVRWindow 实例的问题。

## [1.1.6.4] 2026.05.20

1.增加用户自定义导出


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
