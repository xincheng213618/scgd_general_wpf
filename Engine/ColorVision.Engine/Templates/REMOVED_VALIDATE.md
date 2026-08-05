# Validate 模板项目移除标记

`REMOVED_VALIDATE_TEMPLATE`

`Engine/ColorVision.Engine/Templates/Validate/` 已于 2026-08-06 从主代码中移除。
删除前状态保存在 Git 标签 `archive/validate-before-removal-20260806`，标签指向提交 `68d1873e4`。

需要完整恢复时，先从当前代码创建单独分支，再查找删除提交并执行反向提交：

```powershell
git log --all --grep="REMOVED_VALIDATE_TEMPLATE"
git revert <删除提交>
```

也可以仅查看或取回原目录：

```powershell
git show archive/validate-before-removal-20260806:Engine/ColorVision.Engine/Templates/Validate/TemplateComplyParam.cs
git restore --source archive/validate-before-removal-20260806 -- Engine/ColorVision.Engine/Templates/Validate
```

恢复完整功能时还要一并恢复同一删除提交中的旧节点配置器、菜单发现测试、专题文档和文档入口；不要只恢复源码目录。FlowEngineLib 中的旧 ComplianceMath 节点和枚举不属于本次删除提交。
