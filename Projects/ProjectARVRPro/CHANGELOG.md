# CHANGELOG

## [1.1.7.42] 2026.07.10

1. 保存按钮改用动态主题图标资源，兼容多主题及运行时主题切换。
2. 递增 ProjectARVRPro 版本号并发布。

## [1.1.7.38] 2026.07.06

1. `IProcess.Execute` 和 `ExecuteFailure` 统一改为异步 `Task<bool>` 签名，调用侧可直接等待流程处理完成。
2. ProjectARVRPro 各 Process 实现同步调整为异步接口，移除同步桥接和阻塞等待写法。
3. Demura 成功处理和失败下电均直接走内部异步流程，避免外层封装或同步阻塞。

## [1.1.7.37] 2026.07.06

1. `IProcess` 增加同步失败处理入口 `ExecuteFailure`，失败路径统一交给对应 Process 做解析和收尾。
2. AOI 失败时解析 `findDotsArray` 错误码并写入结构化结果，最终返回使用解析后的失败信息。
3. Demura 流程失败和烧录失败时都会尝试 PG 下电，避免流程节点失败后漏下电。

## [1.1.7.36] 2026.07.05

1. 递增 ProjectARVRPro 版本号并发布。

## [1.1.7.35] 2026.07.04

1. 修复 ARVRPro 窗口关闭后 `ImageView` 未释放导致反复打开窗口时内存叠加的问题。
2. 关闭窗口时断开结果列表、流程分组事件、雷鸟调试窗口和异步载图/截图回调引用。

## [1.1.7.34] 2026.07.04

1. 移除 ARVRPro 结果界面的多图组展示逻辑，结果选中时仅打开当前结果的 `FileName` 单图。
2. 修复 Black_Test 等普通流程因同批次存在多张拍摄图而显示 `1/2` 并拖慢结果切换的问题。

## [1.1.7.33] 2026.07.02

1. Demura增加W128/W255曝光时间配置，默认均为1。
2. 准备G128.csv/G255.csv时会把CSV数值分别除以对应曝光时间，再用于生成bin和烧录。
3. Demura结果文本和测试项增加W128/W255曝光时间，日志记录CSV曝光校准输入、输出和数值数量。

## [1.1.7.32] 2026.07.02

1. Demura烧录命令头长度改为动态计算：按 `PG...` 到 `[03]` 前一位的ASCII字节数生成4位十六进制长度，例如长路径SENDFILE自动生成 `007C`。
2. 上电、SENDFILE、下电统一通过长度前缀组包，发送hex继续写入日志和结果文本。

## [1.1.7.31] 2026.07.02

1. Demura烧录上电指令改为只发送一次：PowerOn -> SendFile -> PowerOff，三步均异步等待对应回包。

## [1.1.7.30] 2026.07.02

1. ARVR自定义流程执行入口改为异步等待，Demura烧录TCP连接、发送、读回包全部使用async I/O，避免等待PG回包时锁死界面。
2. Demura烧录日志和结果增加发送命令hex，确认 `[02][FF]... [03]` 会解析为实际字节发送，例如上电命令为 `02 FF 30 30 30 44 50 47 2C 31 2C 50 4F 57 45 52 2C 4F 4E 03`。

## [1.1.7.29] 2026.07.02

1. Demura烧录改为逐条指令握手：两次上电、SENDFILE烧录、下电都必须收到对应回包后才继续。
2. 下电指令修正为 `[02][FF]000EPG,1,POWER,OFF[03]`，按 `POWER,OFF,END,OK` 判定成功。
3. 烧录结果汇总每一步的回包文本和hex，日志中标明PowerOn1、PowerOn2、SendFile、PowerOff具体失败点。

## [1.1.7.28] 2026.07.02

1. Demura烧录在发送SENDFILE前，先通过同一条TCP/IP连接连续发送两次PG上电指令 `[02][FF]000DPG,1,POWER,ON[03]`。
2. 烧录日志和结果命令文本显示上电与SENDFILE的完整发送顺序，便于现场调试。

## [1.1.7.27] 2026.07.02

1. Demura烧录固定为TCP/IP直连PG，不再提供烧录前关闭/重开通用传感器服务的兼容开关。
2. 烧录异常只通过日志和结果文本输出连接、发送、回包等失败信息。

## [1.1.7.26] 2026.07.02

1. Demura烧录默认改为PG多连接模式：只读取通用传感器的PG地址端口并新建TCP连接，不再默认关闭/重开通用传感器服务。
2. 保留“烧录前关闭服务”兼容开关，PG不支持多连接时可手动启用旧流程。

## [1.1.7.25] 2026.07.02

1. Demura生成bin后接入PG烧录流程：关闭通用传感器服务，直连PG发送SENDFILE指令，等待回包后关闭TCP并重新打开服务。
2. Demura烧录配置支持通用传感器Code/Category、源bin、目标文件名、命令前缀和超时时间。
3. Demura结果文本和测试项增加烧录源文件、PG连接、命令、回包、服务恢复状态，并补充关键调试日志。

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
