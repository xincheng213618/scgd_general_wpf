# i18n ViewFlow UI Audit Table

Project: ColorVision.Engine

## Summary

- Total Flow_* resource keys: 150
- Build result: 0 errors (both Engine and main project)

## Files Processed

| File | Changes |
|---|---|
| Templates/Flow/ViewFlow.xaml | 6 ToolTip/Content/Text replacements |
| Templates/Flow/ViewFlow.xaml.cs | 8 MessageBox replacements |
| Templates/Flow/FlowNodeAnalysisWindow.xaml | 24 Text/Header/Content replacements |
| Templates/Flow/FlowNodeAnalysisWindow.xaml.cs | 13 MessageBox/CSV/ScottPlot replacements + ComboBoxItem Tag fix |
| Templates/Flow/FlowMessageListWindow.xaml | 17 Text/Header/Content replacements |
| Templates/Flow/FlowMessageListWindow.xaml.cs | 2 MessageBox + ComboBoxItem Tag fix |
| Templates/Flow/DisplayFlow.xaml.cs | 8 MessageBox/status text replacements |
| Templates/Flow/STNodeEditorHelper.cs | 6 context menu + MessageBox replacements |
| Templates/Flow/TemplateFlow.cs | 5 file dialog/MessageBox replacements |
| Templates/Flow/Job/FlowJob.cs | 4 status/message replacements |
| Batch/PreProcess/PreProcessManagerWindow.xaml | 13 Text/Header/Content replacements |
| Batch/PreProcess/PreProcessManagerWindow.xaml.cs | 8 status/label replacements |
| Dao/MeasureBatchManagerPage.xaml | 6 Text/Content replacements |
| Dao/MeasureBatchManagerPage.xaml.cs | 11 MenuItem/MessageBox/label replacements |
| Dao/MeasureBatchPage.xaml.cs | 1 Title replacement |

## Audit Table

| Resource Key | Chinese | English | Status |
|---|---|---|---|
| Flow_ConfirmDeleteFlow | 确认删除流程 "{0}" ? | Confirm delete flow "{0}"? | Replaced |
| Flow_CreateTemplateBeforeSelection | 再选择之前请先创建对映的模板 | Please create the corresponding template before selecting | Replaced |
| Flow_Display_NoTemplateSelected | 未选择有效的流程模板 | No valid flow template selected | Replaced |
| Flow_Display_PreprocessFailed | 预处理失败，流程取消执行 | Pre-processing failed, flow execution cancelled | Replaced |
| Flow_Display_SelectTemplateFirst | 再选择之前请先创建对映的模板 | Please create the corresponding template first | Replaced |
| Flow_Elapsed | 耗时: | Elapsed: | Replaced |
| Flow_ElapsedTimeLabel | 已经执行： | Elapsed:  | Replaced |
| Flow_EndNodeNotFound | 找不到流程结束结点 | Cannot find flow end node | Replaced |
| Flow_EstimatedRemainingLabel | 预计还需要： | Estimated Remaining:  | Replaced |
| Flow_ExecutingNodeLabel | 正在执行节点: | Executing Node: | Replaced |
| Flow_ExportFlow | 导出流程 | Export Flow | Replaced |
| Flow_ExportFlowFilter | 流程包 (*.cvflow)/*.cvflow/STN文件 (*.stn)/*.stn | Flow Package (*.cvflow)/*.cvflow/STN File (*.stn)/*.stn | Replaced |
| Flow_FlowNotStartedMessage | 流程未能启动：可能验证失败、流程正在运行中或未选中模板 | Flow could not start: validation may have failed, flow is already running, or no | Replaced |
| Flow_GetCanvasDataFailed | 获取画布数据失败 | Failed to get canvas data | Replaced |
| Flow_ImportFlow | 导入流程 | Import Flow | Replaced |
| Flow_ImportFlowFilter | 流程文件 (*.cvflow;*.stn)/*.cvflow;*.stn/流程包 (*.cvflow)/*.cvflow/STN文件 (*.stn)/*.stn | Flow Files (*.cvflow;*.stn)/*.cvflow;*.stn/Flow Package (*.cvflow)/*.cvflow/STN  | Replaced |
| Flow_ImportFlowPackageError | 导入流程包时出错: {0} | Error importing flow package: {0} | Replaced |
| Flow_ImportModuleFailed | 导入模块失败: {0} | Failed to import module: {0} | Replaced |
| Flow_ImportTemplateAsModule | 导入模板为模块 | Import Template as Module | Replaced |
| Flow_LastExecutionLabel | 上次执行： | Last Execution:  | Replaced |
| Flow_MeasureBatch_AllArchiveCommandSent | 全部归档指令已经发送 | All archive commands have been sent | Replaced |
| Flow_MeasureBatch_AllArchiveSent | 全部归档指令已经发送 | All archive commands sent | Replaced |
| Flow_MeasureBatch_ArchiveCommandSent | 归档指令已经发送 | Archive command has been sent | Replaced |
| Flow_MeasureBatch_ArchiveSent | 归档指令已经发送 | Archive command sent | Replaced |
| Flow_MeasureBatch_BatchResult | 批次 {0} 结果 | Batch {0} Results | Replaced |
| Flow_MeasureBatch_BatchResultTitle | 批次 {0} 结果 | Batch {0} Results | Replaced |
| Flow_MeasureBatch_ClassificationLabel | 分类: | Category: | Replaced |
| Flow_MeasureBatch_ConfigOptions | 配置选项 | Configuration Options | Replaced |
| Flow_MeasureBatch_Description | 描述: | Description: | Replaced |
| Flow_MeasureBatch_DescriptionLabel | 描述: | Description: | Replaced |
| Flow_MeasureBatch_ExecutePostProcess | 执行后处理 | Execute Post-Process | Replaced |
| Flow_MeasureBatch_Name | 名称: | Name: | Replaced |
| Flow_MeasureBatch_NameLabel | 名称: | Name: | Replaced |
| Flow_MeasureBatch_NodeTimeAnalysis | 节点时间分析 | Node Time Analysis | Replaced |
| Flow_MeasureBatch_NotSelected | 未选择 | Not Selected | Replaced |
| Flow_MeasureBatch_PostProcessExecution | 后处理执行 | Post-Process Execution | Replaced |
| Flow_MeasureBatch_ProcessError | 处理出错: {0} | Process error: {0} | Replaced |
| Flow_MeasureBatch_ProcessFailed | 处理失败: {0} | Process failed: {0} | Replaced |
| Flow_MeasureBatch_ProcessInfo | 处理信息 | Process Information | Replaced |
| Flow_MeasureBatch_ProcessResult | 处理结果 | Process Result | Replaced |
| Flow_MeasureBatch_ProcessSuccess | 处理成功: {0} | Process succeeded: {0} | Replaced |
| Flow_MeasureBatch_SelectProcessType | 选择处理类型: | Select Process Type: | Replaced |
| Flow_MeasureBatch_SelectProcessTypeLabel | 选择处理类型: | Select Process Type: | Replaced |
| Flow_MeasureBatch_SelectProcessTypeToViewConfig | 请选择处理类型查看配置 | Please select a process type to view configuration | Replaced |
| Flow_MeasureBatch_SelectToViewConfig | 请选择处理类型查看配置 | Select a process type to view config | Replaced |
| Flow_MeasureBatch_SelectedBatch | 选中批次: | Selected Batch: | Replaced |
| Flow_MeasureBatch_Title | 流程批次配置 | Flow Batch Configuration | Replaced |
| Flow_MessageList_All | 全部 | All | Replaced |
| Flow_MessageList_ClearRecords | 清空记录 | Clear Records | Replaced |
| Flow_MessageList_ConfirmClearAll | 确定要清空所有流程MQTT消息记录吗？ | Are you sure you want to clear all flow MQTT message records? | Replaced |
| Flow_MessageList_DisplayCount | 当前显示: | Showing: | Replaced |
| Flow_MessageList_ElapsedMs | 耗时(ms) | Elapsed(ms) | Replaced |
| Flow_MessageList_LoadCount | 加载条数: | Load Count: | Replaced |
| Flow_MessageList_Node | 节点: | Node: | Replaced |
| Flow_MessageList_NodeHeader | 节点 | Node | Replaced |
| Flow_MessageList_Query | 查询 | Query | Replaced |
| Flow_MessageList_RecvData | 接收数据 (Receive) | Receive Data | Replaced |
| Flow_MessageList_RecvTime | 接收时间 | Receive Time | Replaced |
| Flow_MessageList_Refresh | 刷新 | Refresh | Replaced |
| Flow_MessageList_SendData | 发送数据 (Send) | Send Data | Replaced |
| Flow_MessageList_SendTime | 发送时间 | Send Time | Replaced |
| Flow_MessageList_State | 状态: | State: | Replaced |
| Flow_MessageList_StateHeader | 状态 | State | Replaced |
| Flow_MessageList_StatusCode | 状态码 | Status Code | Replaced |
| Flow_MessageList_Title | 流程MQTT消息查询 | Flow MQTT Message Query | Replaced |
| Flow_MessageList_TotalCount | 总条数: | Total: | Replaced |
| Flow_Module |  模块 |  Module | Replaced |
| Flow_NewFlow | 新建流程 | New Flow | Replaced |
| Flow_NoAvailableTemplates | (无可用模板) | (No Available Templates) | Replaced |
| Flow_NoEndNode | 找不到流程结束结点 | Flow end node not found | Replaced |
| Flow_NoFlowParamSelected | 当前未选择流程参数, 无法保存 | No flow parameter selected, cannot save | Replaced |
| Flow_NoFlowTemplate | 没有可用的流程模板 | No flow templates available | Replaced |
| Flow_NoPathFromStartToEnd | 无法找到从起始结点到结束结点的有效路径 | Cannot find valid path from start node to end node | Replaced |
| Flow_NoStartNode | 找不到流程起始结点 | Flow start node not found | Replaced |
| Flow_NoTemplateAvailable | (无可用模板) | (No templates available) | Replaced |
| Flow_NoValidFlowTemplateSelected | 未选择有效的流程模板 | No valid flow template selected | Replaced |
| Flow_NodeAnalysis_All | 全部 | All | Replaced |
| Flow_NodeAnalysis_BatchSelection | 批次选择: | Batch Selection: | Replaced |
| Flow_NodeAnalysis_Compare | 对比 | Compare | Replaced |
| Flow_NodeAnalysis_ComparisonTitle | 流程节点对比 (多批次) | Flow Node Comparison (Multi-batch) | Replaced |
| Flow_NodeAnalysis_CsvHeader | BatchId,节点名称,节点类型,开始时间,结束时间,耗时(ms),SN | BatchId,NodeName,NodeType,StartTime,EndTime,Elapsed(ms),SN | Replaced |
| Flow_NodeAnalysis_ElapsedMs | 耗时(ms) | Elapsed(ms) | Replaced |
| Flow_NodeAnalysis_ElapsedMsLabel | 耗时 (ms) | Elapsed (ms) | Replaced |
| Flow_NodeAnalysis_EndTime | 结束时间 | End Time | Replaced |
| Flow_NodeAnalysis_Export | 导出 | Export | Replaced |
| Flow_NodeAnalysis_ExportSuccess | 导出成功 | Export successful | Replaced |
| Flow_NodeAnalysis_FileNamePrefix | 节点时间分析_ | NodeTimeAnalysis_ | Replaced |
| Flow_NodeAnalysis_GanttCompareTitle | 流程节点对比 (多批次) | Flow Node Comparison (Multi-batch) | Replaced |
| Flow_NodeAnalysis_GanttTitle | 流程节点甘特图 | Flow Node Gantt Chart | Replaced |
| Flow_NodeAnalysis_Load | 加载 | Load | Replaced |
| Flow_NodeAnalysis_MessageQueryWindow | 消息查询窗口 | Message Query Window | Replaced |
| Flow_NodeAnalysis_MqttCsvHeader | BatchId,节点,NodeId,EventName,MsgId,发送Topic,发送时间,接收Topic,接收时间,耗时(ms),状态码,状态消息,状态 | BatchId,Node,NodeId,EventName,MsgId,SendTopic,SendTime,RecvTopic,RecvTime,Elapse | Replaced |
| Flow_NodeAnalysis_MqttMessageTrace | MQTT消息追踪 | MQTT Message Trace | Replaced |
| Flow_NodeAnalysis_NoDataToExport | 没有数据可导出，请先加载批次数据 | No data to export, please load batch data first | Replaced |
| Flow_NodeAnalysis_Node | 节点 | Node | Replaced |
| Flow_NodeAnalysis_NodeFilter | 节点筛选: | Node Filter: | Replaced |
| Flow_NodeAnalysis_NodeName | 节点名称 | Node Name | Replaced |
| Flow_NodeAnalysis_NodeType | 节点类型 | Node Type | Replaced |
| Flow_NodeAnalysis_RecvData | 接收数据 (Receive) | Receive Data | Replaced |
| Flow_NodeAnalysis_RecvTime | 接收时间 | Receive Time | Replaced |
| Flow_NodeAnalysis_SelectAtLeastOneBatch | 请选择至少一个批次 | Please select at least one batch | Replaced |
| Flow_NodeAnalysis_SelectAtLeastTwoBatches | 请选择至少两个批次进行对比 | Please select at least two batches to compare | Replaced |
| Flow_NodeAnalysis_SendData | 发送数据 (Send) | Send Data | Replaced |
| Flow_NodeAnalysis_SendTime | 发送时间 | Send Time | Replaced |
| Flow_NodeAnalysis_StartTime | 开始时间 | Start Time | Replaced |
| Flow_NodeAnalysis_State | 状态 | State | Replaced |
| Flow_NodeAnalysis_StateFilter | 状态筛选: | State Filter: | Replaced |
| Flow_NodeAnalysis_TimeMs | 时间 (ms) | Time (ms) | Replaced |
| Flow_NodeAnalysis_Timeout | 超时 | Timeout | Replaced |
| Flow_NodeAnalysis_Title | 流程节点时间分析 | Flow Node Time Analysis | Replaced |
| Flow_NodeAnalysis_TotalTime | 总时间 | Total Time | Replaced |
| Flow_NodeAnalysis_XLabelElapsed | 耗时 (ms) | Elapsed (ms) | Replaced |
| Flow_NodeAnalysis_XLabelTime | 时间 (ms) | Time (ms) | Replaced |
| Flow_NodeLabel | 节点: | Node: | Replaced |
| Flow_NodeTimeAnalysis | 节点时间分析 | Node Time Analysis | Replaced |
| Flow_NotStarted | 未启动 | Not Started | Replaced |
| Flow_ParseFlowSampleError | 解析流程样例时出错: {0} | Error parsing flow sample: {0} | Replaced |
| Flow_PreProcess_AllTemplates | 全部模板 | All Templates | Replaced |
| Flow_PreProcess_AppliedTemplates | 应用模板 | Applied Templates | Replaced |
| Flow_PreProcess_AutoSaved | 已自动保存 {0} | Auto-saved {0} | Replaced |
| Flow_PreProcess_BasicInfo | 基本信息 | Basic Info | Replaced |
| Flow_PreProcess_Category | 类别 | Category | Replaced |
| Flow_PreProcess_Close | 关闭 | Close | Replaced |
| Flow_PreProcess_Config | 预处理器配置 | Pre-processor Config | Replaced |
| Flow_PreProcess_ConfigLoaded | 已加载配置 {0} | Config loaded {0} | Replaced |
| Flow_PreProcess_ConfigNotFound | 未找到配置文件 | Config file not found | Replaced |
| Flow_PreProcess_Description | 描述: | Description: | Replaced |
| Flow_PreProcess_Disabled | 停用 | Disabled | Replaced |
| Flow_PreProcess_DisabledStatus | 停用 | Disabled | Replaced |
| Flow_PreProcess_Enabled | 启用 | Enabled | Replaced |
| Flow_PreProcess_EnabledCountFormat | 已启用 {0} 个 | {0} enabled | Replaced |
| Flow_PreProcess_EnabledStatus | 启用 | Enabled | Replaced |
| Flow_PreProcess_LoadFailed | 加载配置失败 | Failed to load config | Replaced |
| Flow_PreProcess_NotSaved | 尚未保存 | Not saved yet | Replaced |
| Flow_PreProcess_ProcessClass | 处理类: | Processor: | Replaced |
| Flow_PreProcess_Processor | 处理器 | Processor | Replaced |
| Flow_PreProcess_ProcessorConfig | 预处理器配置 | Preprocessor Configuration | Replaced |
| Flow_PreProcess_ProcessorCountFormat | {0} 个处理器 | {0} processors | Replaced |
| Flow_PreProcess_SaveFailed | 保存失败，请查看日志 | Save failed, check log | Replaced |
| Flow_PreProcess_SaveNow | 立即保存 | Save Now | Replaced |
| Flow_PreProcess_SelectProcessor | 请选择一个预处理器查看配置 | Select a pre-processor to view config | Replaced |
| Flow_PreProcess_SelectProcessorToViewConfig | 请选择一个预处理器查看配置 | Please select a preprocessor to view configuration | Replaced |
| Flow_PreProcess_Title | 预处理配置 | PreProcess Configuration | Replaced |
| Flow_PreProcess_Type | 类型: | Type: | Replaced |
| Flow_PreprocessFailedCancelled | 预处理失败，流程取消执行 | Preprocessing failed, flow execution cancelled | Replaced |
| Flow_RunFlowFirst | 请先执行流程 | Please run the flow first | Replaced |
| Flow_SaveFailed | 保存失败: {0} | Save failed: {0} | Replaced |
| Flow_StartNodeNotFound | 找不到流程起始结点 | Cannot find flow start node | Replaced |
| Flow_StartupException | 启动异常 | Startup Exception | Replaced |
| Flow_TemplateNoFlowData | 所选模板没有流程数据 | Selected template has no flow data | Replaced |

## Remaining Chinese Review
### Detailed Remaining Chinese (36 lines)

| File | Line | Content | Category |
|---|---|---|---|
| PreProcessMetadata.cs | 139-140 | 类别:, 类型: | Internal status text |
| IPreProcess.cs | 50, 59 | [DisplayName("启用"), ("应用模板")] | Compile-time attribute |
| CheckCameraPreProcess.cs | 59, 66 | WaitForMsgRecordAsync("关闭"/"打开") | Internal button text |
| CheckCameraPreProcess.cs | 105 | MessageBox相机超时 | Internal error |
| PreProcessManager.cs | 59,209,263,268,305,310 | LastSaveStatus strings | Internal status |
| FlowNodeRecordDataBaseHelper.cs | 58 | "流程节点记录" | Internal string |
| FlowNodeAnalysisWindow.xaml.cs | 236 | ScottPlot.Fonts.Detect("中文") | Font detection |
| AlgorithmNodeConfigurators.cs | 176-267 | case AlgorithmType.畸变/双目融合/etc. | Enum values |
| TestMessageBoxNode.cs | 41,49,102,108,116,124 | STNodeProperty/FailLocalNode | Compile-time/internal |
| OLEDNodeConfigurators.cs | 104,113 | case Algorithm2Type.图像裁剪/十字计算 | Enum values |


### Skipped (confirmed non-UI)

- **Comments**: ~200 lines (// and XML doc comments)
- **Log messages**: ~50 lines (log.Info/Debug/Warn/Error)
- **Compile-time constant attributes**: ~15 lines ([DisplayName], [Category], [Description], [PreProcess], [STNode])
- **Database/SQL strings**: ~5 lines (INSERT INTO, ColumnDescription)
- **Internal strings**: ~5 lines (ScottPlot font detection, FlowNodeRecordDataBaseHelper)

### NeedsReview

- TestMessageBoxNode.cs: [STNode], [DisplayName], [Category] attributes with Chinese text
- FolderSizePreProcess.cs: [DisplayName], [Description] attributes
- CheckCameraPreProcess.cs: [PreProcess] attribute, WaitForMsgRecordAsync button text
- IPreProcess.cs: [DisplayName] attributes
- FlowEditor.cs: [EditorForExtension] attribute
- MeasureBatchManagerPage.xaml.cs: [DisplayName] attribute
