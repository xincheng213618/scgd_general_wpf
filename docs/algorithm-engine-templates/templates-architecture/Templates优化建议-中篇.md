# Templates 模块优化建议 - 中篇：模板分类与组织优化

## 目录
1. [概述](#概述)
2. [当前组织结构分析](#当前组织结构分析)
3. [目录结构优化](#目录结构优化)
4. [模板分类优化](#模板分类优化)
5. [命名规范改进](#命名规范改进)
6. [代码组织优化](#代码组织优化)
7. [实施建议](#实施建议)

## 概述

本文档是Templates模块优化建议的中篇，聚焦于模板的分类、组织和代码结构优化。通过合理的分类和组织，可以显著提升代码的可读性、可维护性和可扩展性。

### 优化目标

- **清晰的分类**: 按功能域合理分组
- **一致的命名**: 统一的命名规范
- **合理的结构**: 减少耦合，便于导航
- **易于扩展**: 添加新模板时的便利性

## 当前组织结构分析

### 现状概览

```
Templates/
├── ARVR/                     # AR/VR算法（5个子模块）
├── POI/                      # POI处理（6个子模块）
├── Jsons/                    # JSON模板（16个子模块）
├── Compliance/               # 合规性分析
├── DataLoad/                 # 数据加载
├── FindLightArea/            # 光区查找
├── Flow/                     # 流程模板
├── FocusPoints/              # 焦点
├── ImageCropping/            # 图像裁剪
├── JND/                      # 最小可察觉差异
├── LEDStripDetection/        # LED灯带检测
├── LedCheck/                 # LED检查
├── Matching/                 # 匹配
├── Menus/                    # 菜单
├── SysDictionary/            # 系统字典
├── Validate/                 # 验证
└── [核心文件]               # ITemplate.cs等
```

### 存在的问题

#### 1. 分类混乱

**问题**: 
- ARVR和Jsons中有重复的算法（MTF、FOV、Ghost等既有ARVR版本，又有Jsons/MTF2版本）
- 模板按实现方式分类（Jsons/），而非功能域
- 核心文件与功能模块混在同一层级

**影响**:
- 开发者难以快速定位代码
- 容易产生代码重复
- 不便于理解系统结构

#### 2. 命名不一致

**问题**:
- 有的用大写（ARVR、POI、JND）
- 有的用驼峰（ImageCropping、FindLightArea）
- 有的用缩写（JND、POI）
- 版本号命名不统一（MTF vs MTF2）

#### 3. 模块职责不清

**问题**:
- Menus文件夹仅包含菜单定义，应该与模板代码放在一起
- SysDictionary、Validate等通用组件与业务模板混在一起

## 目录结构优化

### 推荐的新结构

```
Templates/
├── Core/                           # 核心抽象层
│   ├── Abstractions/              # 接口和抽象类
│   │   ├── ITemplate.cs
│   │   ├── ITemplateAsync.cs
│   │   ├── ITemplateFactory.cs
│   │   └── ITemplateRepository.cs
│   ├── Base/                      # 基类
│   │   ├── ParamModBase.cs
│   │   ├── ModelBase.cs
│   │   └── TemplateBase.cs
│   ├── Services/                  # 核心服务
│   │   ├── TemplateService.cs
│   │   ├── TemplateControl.cs
│   │   └── TemplateInitializer.cs
│   └── Infrastructure/            # 基础设施
│       ├── TypeConverters/
│       ├── Validators/
│       └── Extensions/
│
├── Optical/                       # 光学测试模板组
│   ├── MTF/                      # 调制传递函数
│   │   ├── Models/
│   │   │   ├── MTFParam.cs
│   │   │   └── AlgResultMTFModel.cs
│   │   ├── Services/
│   │   │   └── AlgorithmMTF.cs
│   │   ├── Views/
│   │   │   ├── DisplayMTF.xaml
│   │   │   └── ViewHandleMTF.cs
│   │   ├── TemplateMTF.cs
│   │   └── MenuMTFParam.cs
│   │
│   ├── SFR/                      # 空间频率响应
│   ├── FOV/                      # 视场角
│   ├── Distortion/               # 畸变
│   └── Ghost/                    # 鬼影
│
├── FeatureDetection/             # 特征检测模板组
│   ├── POI/                      # 兴趣点
│   │   ├── Core/                 # POI核心
│   │   ├── Algorithms/           # 检测算法（原AlgorithmImp）
│   │   ├── Builders/             # 构建器（原BuildPoi）
│   │   ├── Filters/              # 过滤器（原POIFilters）
│   │   ├── Calibration/          # 校准（原POIGenCali）
│   │   ├── Revision/             # 修正（原POIRevise）
│   │   ├── Output/               # 输出（原POIOutput）
│   │   └── TemplatePoi.cs
│   │
│   ├── EdgeDetection/            # 边缘检测
│   └── CornerDetection/          # 角点检测
│
├── ImageProcessing/              # 图像处理模板组
│   ├── LEDStrip/                 # LED灯带（合并Detection和Check）
│   │   ├── Detection/
│   │   │   ├── LEDStripDetectionParam.cs
│   │   │   └── TemplateLEDStripDetection.cs
│   │   └── Checking/
│   │       ├── LedCheckParam.cs
│   │       └── TemplateLedCheck.cs
│   │
│   ├── Cropping/                 # 图像裁剪（原ImageCropping）
│   ├── Enhancement/              # 图像增强
│   └── Transformation/           # 图像变换
│
├── Analysis/                     # 分析模板组
│   ├── Compliance/               # 合规性分析
│   │   ├── Models/
│   │   ├── ComplianceYParam.cs
│   │   ├── ComplianceXYZParam.cs
│   │   └── TemplateCompliance.cs
│   │
│   ├── JND/                      # 最小可察觉差异
│   ├── Matching/                 # 匹配算法
│   └── Quality/                  # 质量评估
│
├── Workflow/                     # 工作流模板组
│   ├── Flow/                     # 流程定义
│   ├── DataLoad/                 # 数据加载
│   └── Batch/                    # 批处理
│
├── Utilities/                    # 实用工具
│   ├── FindLightArea/           # 光区查找
│   ├── FocusPoints/             # 焦点定位
│   └── Common/                   # 通用工具
│
├── UI/                           # UI组件
│   ├── Windows/                  # 窗口
│   │   ├── TemplateManagerWindow.xaml
│   │   ├── TemplateEditorWindow.xaml
│   │   └── TemplateCreate.xaml
│   ├── Controls/                 # 控件
│   └── Menus/                    # 菜单（原Menus文件夹）
│
└── Data/                         # 数据和配置
    ├── Repositories/             # 数据仓储
    ├── Models/                   # 数据模型
    │   ├── ModMasterModel.cs
    │   └── ModDetailModel.cs
    └── SysDictionary/            # 系统字典
```

### 结构优化说明

#### 1. 按功能域分组

**Optical（光学测试）**
- 所有光学性能测试相关的模板
- MTF、SFR、FOV、Distortion、Ghost
- 统一的光学测试接口和基类

**FeatureDetection（特征检测）**
- POI及其相关处理流程
- 其他特征检测算法
- 便于添加新的特征检测类型

**ImageProcessing（图像处理）**
- 基础图像处理算法
- 增强、变换、滤波等
- 与高级算法（Optical）区分

**Analysis（分析）**
- 数据分析、质量评估
- 合规性检测
- 统计分析

**Workflow（工作流）**
- 流程编排
- 批处理
- 数据加载

#### 2. 分离关注点

**Core（核心）**
- 抽象、基类、服务
- 不包含具体业务逻辑
- 高复用性

**UI（用户界面）**
- 所有UI相关代码集中
- 与业务逻辑分离
- 便于UI框架迁移

**Data（数据）**
- 数据访问层
- 数据模型
- 配置数据

## 模板分类优化

### 1. 版本管理策略

**问题**: 当前MTF、MTF2并存，版本策略不清晰

**优化方案**: 明确的版本管理策略

```
方案A：使用命名空间版本化
Templates.Optical.MTF.V1
Templates.Optical.MTF.V2

方案B：使用特性标记（推荐）
[TemplateVersion("1.0")]
public class TemplateMTF : ITemplate<MTFParam>
{
}

[TemplateVersion("2.0")]
[ObsoleteTemplate("1.0")]  // 指明替代的版本
public class TemplateMTFV2 : ITemplate<MTFParam>
{
}

方案C：统一接口，内部版本切换
public class TemplateMTF : ITemplate<MTFParam>
{
    public TemplateVersion Version { get; set; } = TemplateVersion.Latest;
    
    protected override IAlgorithm GetAlgorithm()
    {
        return Version switch
        {
            TemplateVersion.V1 => new MTFAlgorithmV1(),
            TemplateVersion.V2 => new MTFAlgorithmV2(),
            _ => new MTFAlgorithmV2()
        };
    }
}
```

**推荐**: 方案B + 方案C组合
- 新版本独立文件，便于AB测试
- 通过特性标记关系
- 提供统一入口（方案C）供外部使用

### 2. Jsons目录重组

**当前问题**: Jsons目录包含多种不同类型的模板

**优化方案**: 按功能域重新分配

```
Jsons/MTF2        → Optical/MTF/V2/
Jsons/FOV2        → Optical/FOV/V2/
Jsons/Ghost2      → Optical/Ghost/V2/
Jsons/Distortion2 → Optical/Distortion/V2/

Jsons/BinocularFusion → Optical/BinocularFusion/
Jsons/BlackMura       → Analysis/DisplayQuality/BlackMura/
Jsons/HDR             → ImageProcessing/HDR/

Jsons/PoiAnalysis     → FeatureDetection/POI/Analysis/
Jsons/BuildPOIAA      → FeatureDetection/POI/Builders/AA/

Jsons/LargeFlow       → Workflow/Flow/Large/
```

### 3. 模板分类标准

建立清晰的分类标准：

```csharp
public enum TemplateCategory
{
    Optical,           // 光学测试
    FeatureDetection,  // 特征检测
    ImageProcessing,   // 图像处理
    Analysis,          // 分析
    Workflow,          // 工作流
    Utility            // 实用工具
}

public enum TemplateSubCategory
{
    // Optical
    MTF, SFR, FOV, Distortion, Ghost,
    
    // FeatureDetection
    POI, Edge, Corner, Blob,
    
    // ImageProcessing
    Enhancement, Filtering, Transformation, Segmentation,
    
    // Analysis
    Compliance, Quality, Statistics,
    
    // Workflow
    Flow, Batch, Pipeline
}

[TemplateInfo(
    Category = TemplateCategory.Optical,
    SubCategory = TemplateSubCategory.MTF,
    Version = "2.0",
    Description = "调制传递函数测试")]
public class TemplateMTF : ITemplate<MTFParam>
{
}
```

## 命名规范改进

### 1. 统一命名约定

#### 类命名

```csharp
// 模板类：Template + 算法名
public class TemplateMTF { }
public class TemplatePOI { }
public class TemplateCompliance { }

// 参数类：算法名 + Param
public class MTFParam { }
public class POIDetectionParam { }

// 结果类：AlgResult + 算法名 + Model
public class AlgResultMTFModel { }
public class AlgResultPOIModel { }

// 算法类：Algorithm + 算法名
public class AlgorithmMTF { }
public class AlgorithmPOI { }

// 视图处理类：ViewHandle + 算法名
public class ViewHandleMTF { }
public class ViewHandlePOI { }

// 菜单类：Menu + 算法名 + Param
public class MenuMTFParam { }
public class MenuPOIParam { }
```

#### 文件命名

```
// 一致使用PascalCase
TemplateMTF.cs       ✓
templateMTF.cs       ✗

// 匹配类名
TemplateMTF.cs       ✓
MTFTemplate.cs       ✗

// UI文件
DisplayMTF.xaml      ✓
MTFDisplay.xaml      ✗
```

#### 目录命名

```
// 功能域：PascalCase
Optical/             ✓
optical/             ✗

// 具体算法：UPPERCASE（缩写）或PascalCase（全名）
MTF/                 ✓（缩写）
Distortion/          ✓（全名）
mtf/                 ✗

// 子目录：PascalCase
Models/              ✓
Services/            ✓
models/              ✗
```

### 2. 缩写规范

建立统一的缩写词典：

```
MTF  - Modulation Transfer Function（调制传递函数）
SFR  - Spatial Frequency Response（空间频率响应）
FOV  - Field of View（视场角）
POI  - Point of Interest（兴趣点）
ROI  - Region of Interest（感兴趣区域）
JND  - Just Noticeable Difference（最小可察觉差异）
HDR  - High Dynamic Range（高动态范围）
LED  - Light Emitting Diode（发光二极管）

// 避免混淆的命名
LedCheck  → LEDCheck        ✓
ledcheck  → LEDCheck        ✓
LED_Check → LEDCheck        ✓
```

### 3. 版本命名

```csharp
// 不推荐：文件名体现版本
TemplateMTF2.cs      ✗
TemplateMTFV2.cs     ✗

// 推荐：目录体现版本
Optical/MTF/V1/TemplateMTF.cs    ✓
Optical/MTF/V2/TemplateMTF.cs    ✓

// 或使用命名空间
namespace ColorVision.Engine.Templates.Optical.MTF.V1
{
    public class TemplateMTF { }
}

namespace ColorVision.Engine.Templates.Optical.MTF.V2
{
    public class TemplateMTF { }
}
```

## 代码组织优化

### 1. 单一模板的标准结构

每个模板应包含以下标准组件：

```
MTF/
├── Models/                    # 数据模型
│   ├── MTFParam.cs           # 参数模型
│   ├── AlgResultMTFModel.cs  # 结果模型
│   └── MTFOptions.cs         # 配置选项
│
├── Services/                  # 服务和算法
│   ├── AlgorithmMTF.cs       # 算法实现
│   ├── MTFValidator.cs       # 参数验证
│   └── MTFCalculator.cs      # 计算服务
│
├── Views/                     # 视图
│   ├── DisplayMTF.xaml       # 显示控件
│   ├── DisplayMTF.xaml.cs
│   ├── ViewHandleMTF.cs      # 结果处理
│   └── MTFChartView.xaml     # 图表视图
│
├── Data/                      # 数据访问
│   ├── AlgResultMTFDao.cs    # 数据访问对象
│   └── MTFRepository.cs      # 仓储
│
├── TemplateMTF.cs            # 模板主类
├── MenuMTFParam.cs           # 菜单定义
└── README.md                 # 模板说明文档
```

### 2. 共享代码提取

**问题**: 多个模板间有重复代码

**优化**: 提取到共享层

```
Core/
├── Shared/
│   ├── ROI/                  # ROI处理（ARVR模板共享）
│   │   ├── ROISelector.cs
│   │   ├── ROIValidator.cs
│   │   └── ROIDrawer.cs
│   │
│   ├── Imaging/              # 图像处理基础
│   │   ├── ImageLoader.cs
│   │   ├── ImageConverter.cs
│   │   └── ImageValidator.cs
│   │
│   └── Charting/             # 图表绘制（MTF、SFR共享）
│       ├── ChartHelper.cs
│       ├── CurveDrawer.cs
│       └── DataVisualizer.cs
```

### 3. 接口统一

为相似功能的模板定义统一接口：

```csharp
// 光学测试模板的统一接口
public interface IOpticalTemplate : ITemplate
{
    ROIParam ROI { get; set; }
    double Gamma { get; set; }
    Task<OpticalTestResult> ExecuteTestAsync();
}

// 所有光学模板实现此接口
public class TemplateMTF : ITemplate<MTFParam>, IOpticalTemplate
{
    public ROIParam ROI { get; set; }
    public double Gamma { get; set; }
    
    public async Task<OpticalTestResult> ExecuteTestAsync()
    {
        // ...
    }
}
```

### 4. 文档组织

每个模板目录应包含README.md：

```markdown
# MTF 模板

## 概述
调制传递函数（MTF）测试模板，用于评估光学系统的成像质量。

## 参数说明
- **Gamma**: Gamma校正值，范围0-2
- **ROI**: 感兴趣区域，包含X、Y、Width、Height

## 使用示例
\`\`\`csharp
var template = new TemplateMTF();
template.Load();
var param = template.Params[0];
param.Gamma = 0.45;
\`\`\`

## 依赖
- OpenCvSharp4
- cvColorVision

## 相关文档
- [MTF算法说明](../../../../docs/algorithms/mtf.md)
- [光学测试指南](../../../../docs/guides/optical-testing.md)
```

## 实施建议

### 阶段一：规划与准备（1周）

1. **制定详细迁移计划**
   ```
   - 列出所有需要移动的文件
   - 确定依赖关系
   - 制定回滚方案
   ```

2. **建立新目录结构**
   ```bash
   # 创建新目录
   mkdir -p Templates/Core/{Abstractions,Base,Services,Infrastructure}
   mkdir -p Templates/Optical/{MTF,SFR,FOV,Distortion,Ghost}
   mkdir -p Templates/FeatureDetection/POI/{Core,Algorithms,Builders,Filters}
   # ...
   ```

3. **更新构建脚本**
   - 修改.csproj文件
   - 更新CI/CD配置

### 阶段二：逐步迁移（3-4周）

#### 第1周：核心组件
```
1. 移动Core相关文件
   - ITemplate.cs → Core/Abstractions/
   - ModelBase.cs → Core/Base/
   - TemplateControl.cs → Core/Services/

2. 更新命名空间
   namespace ColorVision.Engine.Templates
   → namespace ColorVision.Engine.Templates.Core.Abstractions

3. 修复引用
```

#### 第2周：Optical模块
```
1. 迁移ARVR目录
   - ARVR/MTF/ → Optical/MTF/V1/
   - Jsons/MTF2/ → Optical/MTF/V2/
   
2. 统一命名和结构
3. 提取共享代码到Core/Shared/
```

#### 第3周：其他业务模块
```
1. 迁移POI相关
2. 迁移图像处理相关
3. 迁移分析相关
```

#### 第4周：UI和收尾
```
1. 整理UI组件
2. 更新文档
3. 全面测试
```

### 阶段三：优化与清理（1-2周）

1. **代码审查**
   - 检查所有命名是否符合规范
   - 验证目录结构是否合理

2. **性能测试**
   - 确保重构没有引入性能问题
   - 优化加载速度

3. **文档完善**
   - 更新所有README
   - 补充迁移指南

### 迁移工具脚本

创建自动化脚本辅助迁移：

```powershell
# MigrateTemplates.ps1

param(
    [string]$SourceRoot = "Templates",
    [string]$TargetRoot = "Templates_New"
)

# 迁移映射
$migrations = @{
    "ARVR/MTF" = "Optical/MTF/V1"
    "Jsons/MTF2" = "Optical/MTF/V2"
    "ARVR/SFR" = "Optical/SFR/V1"
    # ... 更多映射
}

foreach ($migration in $migrations.GetEnumerator()) {
    $source = Join-Path $SourceRoot $migration.Key
    $target = Join-Path $TargetRoot $migration.Value
    
    Write-Host "Migrating $source to $target"
    
    # 创建目标目录
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    
    # 复制文件
    Copy-Item -Path "$source\*" -Destination $target -Recurse
    
    # 更新命名空间
    Get-ChildItem -Path $target -Filter "*.cs" -Recurse | ForEach-Object {
        $content = Get-Content $_.FullName
        $newNamespace = "ColorVision.Engine.Templates.$($migration.Value -replace '/','.'')"
        $content = $content -replace "namespace ColorVision\.Engine\.Templates\.(ARVR|Jsons)\.", "namespace $newNamespace."
        Set-Content $_.FullName $content
    }
}
```

### 风险控制

#### 1. 向后兼容

```csharp
// 在旧位置保留别名
namespace ColorVision.Engine.Templates.ARVR.MTF
{
    [Obsolete("Use ColorVision.Engine.Templates.Optical.MTF.V1.TemplateMTF instead")]
    public class TemplateMTF : Optical.MTF.V1.TemplateMTF
    {
    }
}
```

#### 2. 渐进式迁移

```
阶段1: 新结构与旧结构并存
阶段2: 新功能只在新结构中添加
阶段3: 逐步迁移旧代码
阶段4: 删除旧结构（标记为Obsolete后至少保留2个版本）
```

#### 3. 回滚计划

```
- Git分支管理：feature/template-reorganization
- 每个阶段一个commit
- 保留完整的迁移记录
- 必要时可快速回滚
```

## 总结

模板分类与组织优化的核心要点：

1. **按功能域分组**: Optical、FeatureDetection、ImageProcessing等
2. **统一命名规范**: Template前缀、Param后缀、PascalCase
3. **清晰的版本管理**: V1/V2目录或命名空间版本化
4. **标准化模板结构**: Models、Services、Views、Data
5. **提取共享代码**: Core/Shared目录
6. **完善文档**: 每个模板一个README

这些优化将显著提升代码的组织性和可维护性，为后续扩展打下良好基础。

## 相关文档

- [Templates 模块优化建议 - 上篇](./Templates优化建议-上篇.md)
- [Templates 模块优化建议 - 下篇](./Templates优化建议-下篇.md)
- [Templates架构设计](./Templates架构设计.md)
