# 发光区旧算法退役记录（2026-10-11）

已移除主程序的 JND 发光区定位模板（字典 31，Threshold / Times / SmoothSize）和 FocusPoints 模板（字典 15），包括模板加载、手动计算适配器及流程面板关联。`AlgorithmFindLightAreaNode` 仍保留，原序列化类型、连线和参数字段保持不变；唯一的 AA JSON 模板（字典 42）直接由“参数模板”的属性编辑器选择和编辑。

通用 `AlgorithmType` 的历史编号 7 / 8 不再可选，后续编号维持原值；加载旧数值仍可识别为不受支持的值，执行时拒绝，不能自动改作其它算子。服务端仓库未修改，也没有清理数据库中的旧字典、模板或历史结果。

共用的发光区结果 DAO、历史显示、POI 保存和本地 RobustV2 检测继续保留。C++ 对应移除 SetBrightAreaParam / FindBrightArea / GetBrightArea / GetRIAandPT，详细导出和恢复边界见原生仓库的 `RETIREMENT-LightArea.md`。

恢复档案位于本机桌面的 `work/Find-legacy-20261011/retirement-20261011/`：包含 `originals/main/`、`originals/native/`、原文件 SHA-256 清单、各仓库变更补丁和恢复说明。档案为私有源码，不能放入对外算法交付包或上传公网。

未来恢复时先校验档案哈希和反向补丁适用性，逐段复核与新代码的冲突；同时恢复模板加载、编辑器关联、通用流程编号、手动适配器、C++ 实现及导出核对清单。核对服务端模板版本与参数、结果契约后重新构建并验证。不能只复制一份旧 DLL，也不能整文件覆盖后续维护。

当前有效用法见仓库知识主题 `docs/04-api-reference/algorithms/templates/find-light-area.md`。
