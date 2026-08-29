# ARVR MTF/FOV 旧模板移除标记

`REMOVED_ARVR_MTF_FOV_TEMPLATES`

`Engine/ColorVision.Engine/Templates/ARVR/MTF/` 和 `Engine/ColorVision.Engine/Templates/ARVR/FOV/` 已于 2026-08-25 从主代码中移除。
其中旧 `MTFParam` 实际承载的是准确度相关参数，并非当前 MTF 2.0 参数；旧 FOV 模板也已停用。当前 MTF 和 FOV 分别由 `Templates/Jsons/MTF2/` 与 `Templates/Jsons/FOV2/` 提供。

删除前状态保存在 Git 标签 `archive/arvr-mtf-fov-before-removal-20260825`，标签指向提交 `8731cfc39`。

需要完整恢复时，先从当前代码创建单独分支，再查找删除提交并执行反向提交：

```powershell
git log --all --grep="REMOVED_ARVR_MTF_FOV_TEMPLATES"
git revert <删除提交>
```

也可以仅查看或取回原目录：

```powershell
git show archive/arvr-mtf-fov-before-removal-20260825:Engine/ColorVision.Engine/Templates/ARVR/MTF/TemplateMTF.cs
git show archive/arvr-mtf-fov-before-removal-20260825:Engine/ColorVision.Engine/Templates/ARVR/FOV/TemplateFOV.cs
git restore --source archive/arvr-mtf-fov-before-removal-20260825 -- Engine/ColorVision.Engine/Templates/ARVR/MTF Engine/ColorVision.Engine/Templates/ARVR/FOV
```

恢复完整功能时还要一并恢复同一删除提交中的节点配置入口、项目文件和专题文档；不要只恢复源码目录。恢复后旧模板会与当前 MTF2/FOV2 同时出现，需要重新确认模板选择优先级和结果 handler 的版本匹配。
