# JND 模板项目移除标记

`REMOVED_JND_TEMPLATE`

`Engine/ColorVision.Engine/Templates/JND/` 已于 2026-08-06 从主代码中移除。
删除前状态保存在 Git 标签 `archive/jnd-before-removal-20260806`，标签指向提交 `a35a8243a`。

需要恢复时，先从当前代码创建单独分支，再查找删除提交并执行反向提交：

```powershell
git log --all --grep="REMOVED_JND_TEMPLATE"
git revert <删除提交>
```

也可以仅查看或取回原目录：

```powershell
git show archive/jnd-before-removal-20260806:Engine/ColorVision.Engine/Templates/JND/TemplateJND.cs
git restore --source archive/jnd-before-removal-20260806 -- Engine/ColorVision.Engine/Templates/JND
```

恢复完整功能时还要一并恢复同一删除提交中的节点配置入口和专题文档；不要只恢复源码目录。
