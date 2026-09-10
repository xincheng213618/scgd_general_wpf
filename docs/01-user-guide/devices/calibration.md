---
knowledge_id: "operations.calibration"
knowledge_type: "topic"
status: "current"
summary: "校准服务绑定物理相机并执行本地文件或MQTT校正；模板按Native执行链选用存在的校正文件，输出、显示、落库与缓存删除是不同完成边界。"
aliases: ["校准服务","本地校正","标定资源","校准模板打不开","校正参数设置","四色校正采集","LumFourColorCalibrationSession","CalibrationControl","CalibrationSlotDefinitions","清理校准缓存","UseLocalCalibration","DeviceCalibration","LocalFileCalibrationService","MQTTCalibration"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Calibration/DeviceCalibration.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/ConfigCalibration.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/DisplayCalibration.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/LocalFileCalibrationService.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/MQTTCalibration.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/Views/ViewCalibration.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/InfoCalibration.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/Group/CalibrationParam.cs","Engine/ColorVision.Engine/Services/PhyCameras/Group/CalibrationControl.xaml","Engine/ColorVision.Engine/Services/PhyCameras/Group/CalibrationControl.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/Group/CalibrationSlotDefinitions.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationWorkflow.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationWorkflowWindow.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorPoiEditor.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCalibrationCacheService.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCalibrationCacheManagerWindow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationNode.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CVRawManualCieCalculatorTests.cs","Test/ColorVision.UI.Tests/CalibrationEditorInteractionTests.cs","Test/ColorVision.UI.Tests/LumFourColorWorkflowSafetyTests.cs"]
related: ["engine.devices","operations.device-configuration","operations.physical-camera","flow.session"]
---

# 校准服务、本地文件校正与结果持久化

`DeviceCalibration` 绑定物理相机和校准模板；`DisplayCalibration` 的文件校正默认选择 `UseLocalCalibration=true`。本地后端直接处理输入文件，MQTT 后端请求外部服务处理，两者不是同一个成功判据，也没有失败后自动切换后端的保证。

本页描述已有文件的校正与结果呈现，不把“校准服务”泛化为自动采集或生成所有标定资源。切换真实相机、标定文件和设备参数需确认资源归属及现场验证；问答或文档维护不授权取图、硬件动作、数据库写入、文件删除或缓存清理。

## 相机绑定、模板与配置归属

`ConfigCalibration.CameraCode` 通过 `PhyCameraManager` 解析物理相机。`DeviceCalibration.Save` 在公共设备保存后重新附着相机；`AttachPhyCamera` 解除旧相机事件/反向引用，订阅新相机配置变化并设置校准服务引用。不能只看服务名称判断相机归属，需核对 Code、实际 `PhyCamera` 和模板资源。

`EditCalibration` 当前只显式检查物理相机是否存在，然后打开 `TemplateCalibrationParam` 编辑器；不是“启用 MySQL 但未连接就一定进不了窗口”。数据库依赖在后续模板操作中：模板字典 ID 为 `2`，资源加载以当前相机 `SysResourceModel.Id` 为条件，`CalibrationParam.LoadResourceParams` 在 MySQL 未连接时直接返回。窗口打开、模板列表可用、模板成功保存是三个不同事实。

模板中的“模板校正配置”仅显示文件引用非空的校正项（未启用但已配置的项仍显示），可从当前相机同类型资源中选择或手工输入文件引用，只修改当前模板的文件名和资源 ID，不改写校正组。切换模板保留已保存引用；切换校正组时使用该组的默认文件。顶部“校正组管理”可修改组资源，返回后刷新本机状态。参数区展示当前组增益及曝光、ND、光圈、焦距和对焦距离，后五项不参与模板校正逻辑。

模板行将文件状态和定位/编辑操作共用同一位置：文件缺失时显示“本机缺失”，存在时显示定位与适用的编辑图标，不再额外展示存在状态。悬停文件引用可查看完整文本。空引用项在加载或切换时隐藏；新增组资源仍从“校正组管理”进入。文件状态与启用选择相互独立：“未配置”表示引用为空，“本机缺失”表示当前引用未匹配到本机可访问的资源文件，“本机存在”仅证明文件存在，不证明内容有效或远端服务能访问。状态检查不会取消已保存的勾选；缺失时仍可更换引用、开启或关闭校正项。定位只对本机存在的资源可用；均匀场、DSNU、缺陷点和线性度采用二进制格式，不显示文本编辑入口，`CalibrationResource.Edit` 同样禁止这四类及未知类型。暗噪声、色偏、畸变、ColorDiff、角度偏移及亮度/单色/四色/多色为文本校正类型，可在文件存在时编辑，不按 `.txt` 后缀判断。本地取图和四色窗口带入文件按模板保存的文件引用、校正类型查找资源，不退回校正组默认文件；引用缺失时提示失败。执行前需要核对所启用文件的实际可用性，模板修改仍需点击模板列表下方“保存”。

成像校正的显示、分组保存与 Native 加载共用 `CalibrationSlotDefinitions.NormalSlots`，顺序表示实际执行链而不是 `CalibrationType` 枚举编号：`DarkNoise → DefectPoint → DSNU → Uniformity → ColorShift → Distortion → LineArity → ColorDiff → AngleShift`。Native `CalibrationContext` 按加载顺序执行非色度项，并把互斥的亮度/色度转换延迟到最后；亮度与四色优先显示，单色和多色弱化显示但仍可选择。

## 四色校正采集

“四色校正”默认使用较大的窗口（1600×1060，受屏幕工作区限制），图像与光谱参考并排显示，相机测量值放在侧栏，为 9566×6548 等横向图像保留显示空间。图像等比缩放，横向色块列出两侧状态。单点只需一组；默认“RGBW（MATLAB）”需要四组；“RGB（Python）”只需 R/G/B 三组，采集顺序不限。

1. 选择相机和校正模板时，模板中配置的四色校正文件会自动填入原文件栏；模板启用多色校正时带入对应的多色文件。也可点击“选择文件”手动指定。文件不存在或无法读取会提示，不能静默使用之前模板带入的文件。多设备时不默认选择第一台，模板需显式选择；真机取图要求模板启用与文件格式匹配的四色或多色校正。
2. 选中色块，点击“相机取图”或“选择 CIE”。图像使用 `ImageView`，默认圆形 POI，可切换矩形；拖动绘制后自动读取原始 XYZ/x/y。选中区域后可拖动位置、调整尺寸，通过右键“编辑”设置位置和尺寸，或使用图像内的尺寸面板。“绘制 POI”保留手动画点，每个色块只保留一个区域。选择“POI 模板”并点击“应用”可带入模板的第一个点；后续取图也使用已选模板，仍可手动调整，不修改原模板。模板明确记录的图像尺寸必须匹配，第一个点须为圆形或矩形，不自动跳过无效点。形状及绘图默认尺寸通过 `LumFourColorPoiOptions` 保存；区域超出图像或尺寸无效时清除旧测量并提示调整。
3. “测量数据”中相机、光谱各有 Y、CIE x、CIE y，共六个可编辑值，可直接录入或修改已有测量值。光谱也可点击“采集光谱”，或在“选择已有…”中选择当前光谱仪最近 100 条亮度测量中的一条。历史列表按时间、ID、Y/x/y、IP 和 ND 展示，不默认选中记录；导入前后都限定设备归属，光通量 / EQE 数据不能作为亮度 Y。
4. 当前模式的数据齐全后点击上方“计算校正”。通过核对或明确确认待复核项后，单点与 RGBW 可在底部“另存为”，或点击“替换当前文件并重启服务”；Python RGB 使用“导出 XYZ 矩阵”。

单点和 RGBW 两种模式都自动识别 `a…i` 及 `Gain/pa` 两种 JSON 校正文件，界面显示读到的格式。另存与替换都保持原格式、增益、曝光、位深及其他字段，只替换矩阵系数；“另存为”保留原文件。`pa` 需要九个有限系数，`Gain` 至少三个有限值且前三项非零。含两套矩阵、重复字段、缺项或非法数值的文件会报错，旧版非 JSON 文本暂不支持。格式与对应模板类型见[校正文件兼容契约](../../04-api-reference/engine-components/ColorVision.FileIO.md#四色校正系数转换)。

“RGB（Python）”复现提供的 Python 多色计算：使用 R/G/B 两侧的 Y/x/y，求出从相机 XYZ 到参考 XYZ 的修正矩阵，不使用 W，也不与原校正矩阵合成。原文件仍作为取图和来源核对的依据。导出为独立的 `a…i` JSON，增益为 1、曝光为 0，与 Python 输出一致；即使原文件是 `Gain/pa` 也采用此格式，默认文件名带 `_PythonRGB_XYZ`，不能作为已合成的 RGBW 校正文件直接替换。计算器不依赖 Python 运行环境；零 CIE y、非有限数值和不可逆 RGB 实测矩阵仍会拒绝，不沿用 Python 的零分母补值。

手动录入或修改后，该侧标为“手动值”，并在计算前集中复核。两侧的“恢复”分别恢复原始 POI 或光谱测量值；重新绘制 POI、取图或选择光谱会替换对应侧的手动值。每个色块保存自己的六项输入，未填完整或包含无效数值时不能计算；已有全部数值时不要求图像或设备连接。相机手动值按 Yxy 换算 XYZ，恢复时保留原始 POI 的精确 XYZ；手动光谱参考不虚构曲线。编辑不写回设备、数据库、源图或源记录。

主界面保留输入、采集按钮、简短状态和完成数量；“测量详情”默认折叠，内含图像来源、POI、曝光、原始光谱结果 ID、时间、IP 与波形。原始记录仅供核对，不能证明手动修改后的值已通过质量验证。

光谱 IP 使用原始峰值 AD / 65535 × 100%，工艺范围为 **30%～95%，含端点**。低于范围提示增加积分时间或调整 ND，高于范围提示降低积分时间或调整 ND；缺失 IP 标为无法判断，不补 0 或视为合格。真实取图记录所用四色校正文件的内容指纹，计算时与原文件核对；导入 CVCIE 不含校正模板身份，显示“模板待核对”。范围异常、缺失 IP / 采集时间和模板未核实 / 不一致在计算前集中确认一次，默认取消；选择继续仅表示操作员接受本次风险，不表示程序已验证数据正确。自动亮度预览不能判断相机过曝，仍须核对原图曝光、当前色块、测量位置和 ND。

NaN/Infinity、零 CIE y、XYZ 换算溢出、采集/导入中的非法峰值 AD、空光谱、波长重复 / 倒序，以及多组未修改参考值重复使用同一设备同一结果 ID 会阻止完成或计算。有限负 XYZ、Y/x/y、光谱和矩阵系数原样保留。重采 / 重选一侧会先撤销该侧旧值；失败后不能回用。重画开始即清除旧 POI 读数。更换原文件、相机或相机模板清空相机侧；更换光谱仪清空光谱侧；另一侧保留。切换任一计算模式都会清空测量值、重建会话。任一输入改变或计算失败都会使旧结果失效。

计算与保存前重新核对原文件指纹，源文件被外部修改时须重新选择文件并采集。“另存为”禁止使用原文件路径，临时文件写入并读回校验后再替换目标副本，不重启服务。

“替换当前文件并重启服务”直接操作上方显示的原文件，使用前须确认该路径就是要更新的文件，并安排好服务中断。程序先验证临时结果，再在原目录创建带日期、时间、唯一标识及 `_backup` 后缀的备份，保留原扩展名；备份逐字节内容指纹一致且原文件未变化后，才原子替换原文件并调用现有 ColorVision 服务重启流程。备份或写入失败不会重启服务；重启失败会明确提示文件已替换、需检查服务后手动重启，不自动回滚文件。底部显示备份路径，悬停可查看完整路径。

替换期间禁止重复提交、修改当前窗口和关闭窗口。替换完成后，采集窗口清除旧相机数据并保留光谱参考，手工窗口清空测量表和确认状态，均需重新测量并计算。Python RGB 的独立 XYZ 矩阵不能直接替换原文件，该模式禁用替换按钮。保存不会修改模板指向、上传资源或切换 PG / ND；服务重启完成不等于实际相机取图验收。

“手工数值”使用按单元格选择的 DataGrid：从起始单元格 Ctrl+V 可粘贴 Excel 连续区域，Ctrl+C 复制所选单元格，“复制全部”包含表头与目标名称。数值列按相机 Y/x/y、光谱 Y/x/y 排列，可带相同表头及目标列；带目标时必须与当前行色块一致。超出范围、列数不一致或包含非有限数值时整次拒绝；空单元格用于清空数值，不能直接计算。操作员须先核对色块、原文件和 IP；修改数值或文件路径会撤销确认和旧结果。该入口没有原始采集元数据，不能代替采集窗口的自动核对。

设备连接配置走[公共设备配置持久化](./configuration.md)；`DisplayCalibrationConfig` 则按设备 Code 保存本地显示选择，例如后端和曝光模式。物理相机与标定资源归属见[物理相机](./camera-management.md)，不要以重开编辑器替代资源/数据库核对。

## 文件、曝光与后端选择

手动执行都需要已绑定物理相机、选中 `CalibrationParam` 和输入文件名。普通曝光模式把 R 值用于 R/G/B；`IsAdvancedExposure` 才分别使用三个值。读取到有效 CVRAW/CVCIE 文件头曝光后，界面会展示头信息并限制相应编辑；这不代表输入文件已经成功校正。

| 路径 | 输入和执行条件 | 输出与完成边界 |
| --- | --- | --- |
| 本地 `LocalFileCalibrationService.Calibrate` | 需要关联的 `DeviceCamera`、可解析的校准文件；曝光必须有限且大于 0；加载后必须含 RAW | 执行本地校正，写结果文件，再直接把结果模型交给视图；数据库保存是后续可失败步骤 |
| MQTT `MQTTCalibration.Calibration` | 发送服务可访问的 `ImgFileName`、`FileType`、`TemplateParam.ID/Name`，以及 `DeviceParam.exp=[R,G,B]`、`gain=1` | `Event_GetData` 回包再按 `MasterId` 查数据库结果；回包成功不代表客户端已读到结果文件 |

本地入口支持能加载为 RAW 的 CVRAW、TIFF 或普通位图；当前文件服务对不含 RAW 的 CVCIE 输入会报错并提示切换 MQTT，不自动回退。仅有物理相机条目也不足以启用本地校正，还须有 `PhyCamera.DeviceCamera` 和有效本地校准资源。选择本地后端时，显示入口不以 MQTT 设备已打开为前提；这不免除本地文件和运行库依赖。

MQTT 请求传的是文件名/路径，不是上传文件内容；“本机存在”不能证明服务能访问。`Calibration` 方法虽然接收 `CalibrationParam item`，实际载荷使用单独传入的模板 ID/名称和曝光；定位时以发送载荷为准。

## 结果显示与历史落库不是同一件事

本地校正调用 `LocalFrameFileService.SaveCapture`，按 `Config.FileServerCfg.DataBasePath` 和校准设备 Code 保存输出，优先选择 CVCIE，其次 CVRAW；没有生成可用文件会抛错。随后创建 `MeasureResultImgModel`，记录源文件、模板、曝光和 `Backend` 等信息。

`TryPersist` 在 MySQL 未连接时直接跳过；连接后尝试关联/创建批次并保存图像结果，失败会记日志，但已生成文件仍可用。`RunLocalCalibrationAsync` 可直接 `View.ShowResult(model)`，因此“本地操作完成并显示图像”不证明历史批次和结果记录已写入数据库。需要历史重开能力时必须另核对持久化记录及文件存续。

MQTT 结果入口和 `ResultMessageBus` 的校准图像通知都由 `ViewCalibration` 转到 `ShowPersistedResult`：要求正数 `MasterId`，按 ID 读取 `MeasureResultImgModel`，有记录才显示。消息总线还按校准路由、图像类型和设备 Code 筛选。缺少记录、数据库不可用或 `FileUrl` 不可读，应按不同阶段诊断，而不是笼统归为“校准失败”。

公共 MQTT 消息追踪的 `Success` 只对应匹配请求的 `Code=0`；它不会等待数据库查询和图像加载。不要用按钮完成、消息回包或列表已有旧项互相替代本次结果验收。

## 三种“清理”必须分开

| 入口 | 实际作用与安全边界 |
| --- | --- |
| `ReleaseLocalCalibrationCacheCommand` / 本地校正缓存管理 | 面向所有相机的本地校正上下文与进程级共享文件内存缓存，不是删除磁盘标定文件；等待正在执行的校正结束，仍被其它活动上下文引用的内存不强制释放 |
| `InfoCalibration.ServiceCache_Click` → `MQTTCalibration.CacheClear` | 界面先提示永久删除，再发远端 `Event_Delete_Data`；必须按远端删除操作授权，不能当作无副作用的排障动作 |
| `ViewCalibration` 清空列表/删除结果项 | 从当前 `ViewResults` 移除，不代表删除数据库结果、输出文件或校准缓存 |

远端清理的现有界面在任意 `MsgRecordStateChanged` 时都弹出“清理完成”，没有先判断 `Success`；该提示不能证明删除成功。应核对对应请求的 Code/终态及远端实际结果。本页不宣称已经修复该行为，也不建议因怀疑旧缓存就先执行删除。

当前 `MQTTCalibration` 只封装校正请求与缓存删除，没有独立的原始文件列表/下载方法；不要从服务名称推断这些操作可用。

## Flow 本地校正是独立入口

`Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationNode.cs` 消费流程当前内存帧，或从输入结果/文件路径加载帧；不会读取手动显示的 `UseLocalCalibration` 来决定后端。可复用的 CIE 帧可以直接沿流程传递，RAW 帧按模板校正，所以手动文件入口“不含 RAW 的 CVCIE 报错”不能扩展为整个 Flow 都不支持 CIE。

该节点 `SaveFiles` 默认关闭：内存帧可继续传递不等于已生成结果文件。它的 `SaveCalibrationResult` 需要流程批次，并要求结果保存返回正数 ID，否则抛错；关闭 `SaveFiles` 不会跳过数据库保存。它不采用手动本地服务的“可跳过 MySQL 保存”策略。文件保存、完成通知与整体流程终态须分别核对，参见[Flow 执行会话](../workflow/execution.md)。

## 证据与验证缺口

本页没有声明直接覆盖 `DeviceCalibration` 本地执行、MQTT 结果关联或缓存清理终态的自动化测试。相机模板克隆和上传测试只覆盖各自资源操作，不能作为这条运行链已经通过验证的证据。

验收需在有相应环境与授权后，分别验证校准资源适配、像素结果、输出文件、历史重开和失败清理边界。
