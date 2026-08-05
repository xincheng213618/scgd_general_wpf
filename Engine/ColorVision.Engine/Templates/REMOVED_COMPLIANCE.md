# Compliance 模块移除标记

`REMOVED_COMPLIANCE_TEMPLATE`

`Engine/ColorVision.Engine/Templates/Compliance/` 已于 2026-08-06 从主代码中移除。
删除前状态保存在 Git 标签 `archive/compliance-before-removal-20260806`，标签指向提交 `b7ccc4ed6`。

需要完整恢复时，先从当前代码创建单独分支，再查找删除提交并执行反向提交：

```powershell
git log --all --grep="REMOVED_COMPLIANCE_TEMPLATE"
git revert <删除提交>
```

也可以仅查看或取回原目录：

```powershell
git show archive/compliance-before-removal-20260806:Engine/ColorVision.Engine/Templates/Compliance/ComplianceYModel.cs
git restore --source archive/compliance-before-removal-20260806 -- Engine/ColorVision.Engine/Templates/Compliance
```

恢复完整功能时还要一并恢复同一删除提交中的专题文档和文档入口；不要只恢复源码目录。FlowEngineLib 中的旧流程节点和协议标识不属于本次删除提交。
