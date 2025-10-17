# ProcessManagerWindow UI Layout

## Before (Original):
```
┌─────────────────────────────────────────────────────────────┐
│ 流程处理配置                                                  │
├─────────────────────────────────────────────────────────────┤
│ ┌─ 创建 ProcessMeta ────────────────────────────────────┐   │
│ │ 名称: [____] 流程模板: [____] 处理类: [____] [添加]   │   │
│ └────────────────────────────────────────────────────────┘   │
│                                                               │
│ [删除选中] (双击可重命名, 当前仅内存保存)                     │
│                                                               │
│ ╔═══════════════════════════════════════════════════════╗   │
│ ║ 名称      │ 流程模板    │ 处理类                      ║   │
│ ╠═══════════════════════════════════════════════════════╣   │
│ ║ Process1  │ Template1   │ BlackProcess                ║   │
│ ║ Process2  │ Template2   │ White255Process             ║   │
│ ║ ...                                                    ║   │
│ ╚═══════════════════════════════════════════════════════╝   │
└─────────────────────────────────────────────────────────────┘
```

## After (With Update Feature):
```
┌─────────────────────────────────────────────────────────────┐
│ 流程处理配置                                                  │
├─────────────────────────────────────────────────────────────┤
│ ┌─ 创建 ProcessMeta ────────────────────────────────────┐   │
│ │ 名称: [____] 流程模板: [____] 处理类: [____] [添加]   │   │
│ └────────────────────────────────────────────────────────┘   │
│                                                               │
│ ┌─ 更新 ProcessMeta ────────────────────────────────────┐   │  ← NEW!
│ │ 流程模板: [____] 处理类: [____] [更新]                │   │  ← NEW!
│ └────────────────────────────────────────────────────────┘   │  ← NEW!
│                                                               │
│ [删除选中] (双击可重命名, 当前仅内存保存)                     │
│                                                               │
│ ╔═══════════════════════════════════════════════════════╗   │
│ ║ 名称      │ 流程模板    │ 处理类                      ║   │
│ ╠═══════════════════════════════════════════════════════╣   │
│ ║ Process1  │ Template1   │ BlackProcess                ║   │ ← Select item
│ ║ Process2  │ Template2   │ White255Process             ║   │
│ ║ ...                                                    ║   │
│ ╚═══════════════════════════════════════════════════════╝   │
│                                                               │
│ When item is selected above ↑:                               │
│ - Update section auto-populates with current values         │
│ - User can modify template/process                           │
│ - Click [更新] to save changes                               │
└─────────────────────────────────────────────────────────────┘
```

## Workflow:

### Old Workflow (Delete & Add):
1. User wants to change ProcessMeta's template/process
2. User clicks [删除选中] to delete the ProcessMeta
3. User manually fills in 名称, 流程模板, 处理类 again
4. User clicks [添加] to recreate it
5. ❌ Risk: Might forget exact name or lose data

### New Workflow (Direct Update):
1. User wants to change ProcessMeta's template/process
2. User clicks on the ProcessMeta in the list
3. Update section auto-fills with current values ✓
4. User changes 流程模板 or 处理类
5. User clicks [更新]
6. ✓ Done! Name preserved, changes saved automatically

## Key Improvements:
- ✅ No need to remember and re-type the name
- ✅ Auto-population reduces errors
- ✅ Faster workflow (less clicks)
- ✅ Changes persist automatically
- ✅ Update button disabled when invalid selection
