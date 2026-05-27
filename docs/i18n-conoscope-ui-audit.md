# i18n Conoscope UI Audit Table

Project: Conoscope

## Summary

- Total resource keys: 431
- Build result: 0 errors

## New Keys Added

| Resource Key | Chinese | English | Status |
|---|---|---|---|
| Conoscope_AllFocusPoints | 全部关注点 | All Focus Points | Replaced |
| Conoscope_AngleDegrees | 角度 (°) | Angle (°) | Replaced |
| Conoscope_CalculateAllFocusPoints | 计算全部关注点 | Calculate All Focus Points | Replaced |
| Conoscope_CalculateFocusPoint | 计算关注点: {0} | Calculate Focus Point: {0} | Replaced |
| Conoscope_CircleAngleDegrees | 圆周角度 (°) | Circle Angle (°) | Replaced |
| Conoscope_CircleDistributionTitle | 极角 {0}° {1}圆周分布曲线 | Polar {0}° {1} Circle Distribution | Replaced |
| Conoscope_ClearAllFocusPoints | 清空全部关注点 | Clear All Focus Points | Replaced |
| Conoscope_ClearFocusPoint | 清空关注点 | Clear Focus Point | Replaced |
| Conoscope_ContrastChannel | 对比度 | Contrast | Replaced |
| Conoscope_ContrastNeedsReference | 对比度通道需要先保存白场或黑场基准 | Contrast channel requires saving white or black field reference first | Replaced |
| Conoscope_ContrastReferenceImage | 对比度基准图 | Contrast Reference Image | Replaced |
| Conoscope_CustomUv | 自定义: u={0:F4}, v={1:F4} | Custom: u={0:F4}, v={1:F4} | Replaced |
| Conoscope_CvcieFileFilter | CVCIE 文件 (*.cvcie)/*.cvcie | CVCIE Files (*.cvcie)/*.cvcie | Replaced |
| Conoscope_EditByAngleLength | 按角度/长度编辑... | Edit by Angle/Length... | Replaced |
| Conoscope_FocusPointInfo | {0}  方位 {1:F2}°  极角 {2:F2}°  R {3:F1}px/{4:F2}° | {0}  Azimuth {1:F2}°  Polar {2:F2}°  R {3:F1}px/{4:F2}° | Replaced |
| Conoscope_FocusPointTemplateName | Conoscope关注点_{0} | ConoscopeFocusPoint_{0} | Replaced |
| Conoscope_GlobalReference | 全局基准图: {0} | Global Reference: {0} | Replaced |
| Conoscope_NoGlobalReference | 未保存全局色差基准图 | No Global Color Difference Reference Saved | Replaced |
| Conoscope_OverallAverage | 整体平均 | Overall Average | Replaced |
| Conoscope_PolarDistributionTitle | 方位角 {0}° {1}分布曲线 | Azimuth {0}° {1} Distribution | Replaced |
| Conoscope_SaveGlobalReference | 保存全局基准图 | Save Global Reference | Replaced |
| Conoscope_ScaleCoefficientNotConfigured | 观察相机尺寸系数未配置 | Observation camera scale coefficient not configured | Replaced |
| Conoscope_TestAreaNotDisplayed | 未显示测试区域 | Test area not displayed | Replaced |
| Conoscope_TestAreaSizeInvalid | 测试区域尺寸无效 | Test area size is invalid | Replaced |
| Conoscope_UpdateGlobalReference | 更新全局基准图 | Update Global Reference | Replaced |
| Conoscope_VReferenceImage | v 基准图 | v Reference Image | Replaced |

## Remaining Chinese Review

### Skipped (confirmed non-UI)

- **Log messages**: ~10 lines (log.Info/Debug/Warn/Error in ConoscopePreprocessPipeline.cs, ConoscopeView.Data.cs)
- **Font detection**: 2 lines (ScottPlot.Fonts.Detect in ConoscopeView.ReferencePlot.cs)
- **Internal load descriptions**: 4 lines (ConoscopeGlobalReferenceStore.cs TryLoadMat descriptions)
- **Compile-time constant attributes**: ~50 lines ([Category], [DisplayName], [Description] in ConoscopeConfig.cs, ConoscopeCoordinateAxis.cs, ConoscopeConfigWindow.xaml.cs)
- **MVCamera.cs vendor comments**: ~40 lines
