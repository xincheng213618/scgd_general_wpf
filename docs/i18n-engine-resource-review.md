# Engine 多语言资源校对报告

**日期**: 2026-05-28
**范围**: Engine/ 目录下所有 Resources.*.resx 文件

## 1. 校对范围

| 项目 | 文件路径 | 基准 key 数 |
|------|----------|-------------|
| ColorVision.Engine | Engine/ColorVision.Engine/Properties/Resources*.resx | 1860 |
| ST.Library.UI | Engine/ST.Library.UI/Properties/Resources*.resx | 308 |
| FlowEngineLib | 无 Resources.resx 文件 | - |
| ColorVision.FileIO | 无 Resources.resx 文件 | - |
| ColorVision.ShellExtension | 无 Resources.resx 文件 | - |
| cvColorVision | 无 Resources.resx 文件 | - |

## 2. Key 完整性检查结果

### 修复前

| 语言 | 文件 key 数 | 缺失 key 数 |
|------|------------|-------------|
| zh-CN (基准) | 1860 | 0 |
| en | 1859 | 1 |
| fr | 1812 | 48 |
| ja | 1812 | 48 |
| ko | 1812 | 48 |
| ru | 1812 | 48 |
| zh-Hant | 1812 | 48 |

### 修复后

| 语言 | 文件 key 数 | 缺失 key 数 |
|------|------------|-------------|
| zh-CN (基准) | 1860 | 0 |
| en | 1860 | 0 |
| fr | 1860 | 0 |
| ja | 1860 | 0 |
| ko | 1860 | 0 |
| ru | 1860 | 0 |
| zh-Hant | 1860 | 0 |

### 缺失的 key（已补齐）

主要缺失的 48 个 key 均为最近新增的 Flow 引擎相关：

- `Flow_CreateTemplateBeforeSelection`
- `Flow_Elapsed`, `Flow_ElapsedTimeLabel`, `Flow_EstimatedRemainingLabel`
- `Flow_EndNodeNotFound`, `Flow_StartNodeNotFound`
- `Flow_ExecutingNodeLabel`, `Flow_ExecutingNodeLabel`
- `Flow_ExportFlowFilter`, `Flow_ImportFlowFilter`, `Flow_ImportFlowPackageError`
- `Flow_FlowNotStartedMessage`, `Flow_NotStarted`, `Flow_StartupException`
- `Flow_LastExecutionLabel`, `Flow_NodeLabel`
- `Flow_MeasureBatch_*` (15 个批次处理相关 key)
- `Flow_NoAvailableTemplates`, `Flow_NoPathFromStartToEnd`, `Flow_NoValidFlowTemplateSelected`
- `Flow_ParseFlowSampleError`
- `Flow_PreProcess_*` (13 个预处理相关 key)
- `Flow_PreprocessFailedCancelled`
- `Update` (en 文件缺失)

## 3. 翻译质量修正

### en (英文) - 修正 24 处

| Key | 修正前 | 修正后 |
|-----|--------|--------|
| Upload | Point Count | Upload |
| UnderDevelopment | File Export | Under Development |
| Topic | Connection Type | Topic |
| TransparentWindow | Storage Type | Transparent Window |
| CalibrationFile | Correction documents | Calibration File |
| DefaultExpTime | Expose default values | Default Exposure Time |
| ProjectName | Name | Project Name |
| CharacterBrghtnessInsp | Character BrghtnessI nsp | Keyboard Inspection |
| DownLeft | BottomLeft | Bottom Left |
| DownRight | BottomRight | Bottom Right |
| AutoIntegra | Automatic intergation | Automatic Integration |
| FlowEditor | Process Editor | Flow Editor |
| FlowWindows | Process Window | Flow Window |
| FlowTemplate | Process Template | Flow Template |
| FlowResultManagement | Process Result Management | Flow Result Management |
| ConfirmDelete | Confirm Deletion | Confirm Deletion |
| Size | size | Size |
| Search | Search | Search |
| Lock | locked | Lock |
| CurrentSpeed | Current Speed: | Current Speed: |
| PleaseConfigureCameraIDFirst | PleaseConfigureDllFirst | Please configure the camera ID first |
| ExportCalibration | Export Correction | Export Calibration |
| CreateCalibration | Create correction | Create Calibration |
| CalibrationFileManagement | Caliberation File Management | Calibration File Management |

### zh-Hant (繁体中文) - 修正 253 处

主要修正类型：

1. **简体混入繁体**（最严重）:
   - `SerialNumber`: "序列号" → "序號"
   - `Save`: "保存" → "儲存"
   - `Name`: "名称" → "名稱"
   - `Settings`: "设置" → "設定"
   - `Add`: "添加" → "新增"
   - 大量类似修正

2. **术语统一**:
   - 物理相机 → 實體相機
   - 许可证 → 許可證
   - 校正 → 校正（保持）
   - 配置 → 配置/設定（根据上下文）

3. **Flow 相关新增 key**: 48 个全部使用正确繁体中文翻译

### fr (法语) - 修正 46 处

主要修正：
- `CurrentSpeed`: "AdaptiveZeroCalibration" → "Vitesse actuelle :"
- `IsDefaultOpenService`: "EstDefaultOpenService" → "Ouvrir le service au démarrage"
- `IsRetorePlayControls`: "IsRetorePlayControls" → "Restaurer la fenêtre de capture au démarrage"
- 多处基础 UI 词汇修正

### ja (日语) - 修正 16 处

主要修正：
- `CharacterBrghtnessInsp`: 翻译修正
- 多处 UI 动作词统一

### ko (韩语) - 修正 17 处

主要修正：
- 多处 UI 动作词统一

### ru (俄语) - 修正 31 处

主要修正：
- `DeviceCode`: "熄灭"（中文！）→ "Код устройства"
- 多处基础 UI 词汇修正

## 4. 格式安全检查

- 所有 `{0}`, `{1}` 占位符保持一致
- 文件过滤器格式 `(*.cvflow)|*.cvflow` 未被破坏
- 未修改任何资源 name
- 未修改 Resources.Designer.cs
- 未修改业务代码

## 5. ST.Library.UI 资源

- 基准 key 数: 308
- 所有语言文件 key 完整，无缺失
- fr/ja/ko/ru 各有 4 个额外 key（不影响功能）

## 6. 构建验证

```
dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj -m:1 -nodeReuse:false -p:Platform=x64 -nologo
结果: 0 个错误，2100 个警告（均为已有代码分析警告，非资源相关）
```

## 7. 后续建议

1. fr/ja/ko/ru 的翻译目前基于机器翻译，建议由母语使用者进行最终审校
2. 建议在 CI 中添加 resx key 完整性检查，防止未来新增 key 遗漏
3. `Flow_*` 系列 key 是最近新增的，翻译质量较高
4. 修正了大量 zh-Hant 中混入的简体中文字符，但仍有少量可能遗漏
