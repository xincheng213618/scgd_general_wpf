# 发光区裁剪退役记录（2026-10-22）

恢复标记：`RETIREMENT-ImageCropping-20261022`。

按功能下线要求移除 `ImageCropping` 强类型模板（字典 32，参数 `UnEgde` / `O_Index`）、“发光区裁剪”手动算法及四点配置、专用属性编辑器、`OLEDImageCroppingNode`（“图像裁剪2”）和参数、结果处理器及其调试/CSV 导出入口，并清理专用本地化资源与活动文档。

通用 AI 算法节点的 `AlgorithmType` 编号 10、双输入 AI 算法节点的 `Algorithm2Type` 编号 2 均保留空位，不再提供 `OLED.GetRIAand`。加载旧数值时保留数值并拒绝执行，不能将其解释成相邻算法或回退成 JND。结果编号 29 同样保留空位，其他流程与协议编号保持原值。包含独立 `OLEDImageCroppingNode` 的旧流程需在支持它的旧版本中删除或替换该节点并保存；当前加载器不会自动跳过未知节点或重接连线。

没有清理数据库中的字典 32、模板、历史结果或用户流程，也没有修改外部算法服务、原生算法库或其他裁剪/ROI 功能。旧结果记录仍保留编号 29；当前版本不再提供该类型的专用明细、调试与导出处理器。兼容性测试中的合成画布同步排除该节点，其余节点 payload 保持原字节，合成串联连线重新编号。

本次变更独立提交，提交标题为 `refactor(algorithms): retire luminous area cropping`。查找与恢复（会修改当前工作区，先保存已有改动）：

```powershell
git log --all --oneline --grep='RETIREMENT-ImageCropping-20261022'
git revert <查到的退役提交号>
```

提交的父版本保留完整实现。需要只读查看时使用 `git show <退役提交号>^:Engine/ColorVision.Engine/Templates/ImageCropping/AlgorithmImageCropping.cs`。恢复后须复核模板字典、服务端 `OLED.GetRIAand` 的参数与结果契约，重新构建并验证手动四点、Flow 双输入、历史明细和导出；不要只恢复单个类、枚举名称或旧 DLL。
