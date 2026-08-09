# BuzProduct 模板项目移除标记

`REMOVED_BUZPRODUCT_TEMPLATE`

`Engine/ColorVision.Engine/Templates/BuzProduct/` 已于 2026-08-06 从主代码中移除。
删除前状态保存在 Git 标签 `archive/buzproduct-before-removal-20260806`，标签指向提交 `b7ccc4ed6`。

需要完整恢复时，先从当前代码创建单独分支，再查找删除提交并执行反向提交：

```powershell
git log --all --grep="REMOVED_BUZPRODUCT_TEMPLATE"
git revert <删除提交>
```

也可以仅查看或取回原目录：

```powershell
git show archive/buzproduct-before-removal-20260806:Engine/ColorVision.Engine/Templates/BuzProduct/TemplateBuzProduc.cs
git restore --source archive/buzproduct-before-removal-20260806 -- Engine/ColorVision.Engine/Templates/BuzProduct
```

恢复完整功能时还要一并恢复同一删除提交中的专题文档和文档入口；不要只恢复源码目录。
