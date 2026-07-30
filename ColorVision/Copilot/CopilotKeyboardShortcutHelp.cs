using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed record CopilotKeyboardShortcut(
        string Scope,
        string Keys,
        string Action);

    internal static class CopilotKeyboardShortcutHelp
    {
        internal static IReadOnlyList<CopilotKeyboardShortcut> Entries { get; } =
        [
            new("全局", "Ctrl+/", "打开这份快捷键速查"),
            new("全局", "Alt+P", "打开模型 Profile 选择器"),
            new("全局", "Ctrl+N", "开始新会话"),
            new("全局", "Ctrl+G", "聚焦跨会话搜索"),
            new("全局", "Ctrl+F", "查找当前会话中的可见内容"),
            new("全局", "Ctrl+O", "复制最近一条已完成回答"),
            new("全局", "Ctrl+T", "收起或展开 Agent 任务明细"),
            new("运行中", "Esc", "停止当前回复；Agent 有安全检查点时先暂停，再按一次取消"),
            new("空闲", "Esc Esc", "打开会话回溯点清单，不立即回溯"),
            new("输入框", "Enter", "标准模式发送；多行模式插入换行"),
            new("输入框", "Shift+Enter", "标准模式插入换行；多行模式发送"),
            new("输入框", "Ctrl+Enter", "标准与多行模式均发送"),
            new("输入框", "Tab", "补全命令或引用；Agent 运行中排队后续请求"),
            new("输入框", "Ctrl+R", "搜索当前会话的可见历史请求"),
            new("输入框", "Ctrl+S", "暂存当前草稿；空输入时恢复"),
            new("输入框", "Ctrl+E", "打开本机大段提示词编辑器"),
            new("输入框", "Ctrl+V", "优先粘贴剪贴板图片，否则正常粘贴文本"),
            new("输入框", "↑ / ↓", "移动命令或引用候选；空输入时浏览历史"),
            new("输入框", "@", "关联文件、模板或菜单"),
            new("输入框", "/ 或 $", "打开命令与 Skill 候选"),
            new("历史搜索", "Ctrl+S", "在当前会话与全部会话之间切换范围"),
            new("会话查找", "Enter / Shift+Enter", "定位下一项或上一项匹配"),
            new("侧栏搜索", "↑ / ↓", "移动会话候选，不立即切换"),
            new("侧栏搜索", "Enter", "确认切换到高亮会话"),
            new("侧栏搜索", "Ctrl+R", "重命名高亮会话，不切换"),
            new("搜索与弹层", "Esc", "关闭当前搜索、候选或编辑状态"),
        ];

        internal static string Format()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Copilot 快捷键 · {Entries.Count}");
            builder.AppendLine();
            builder.AppendLine("同一按键由当前焦点和运行状态解释；以下操作均不会绕过权限或确认。");

            foreach (var group in Entries.GroupBy(entry => entry.Scope))
            {
                builder.AppendLine();
                builder.AppendLine(group.Key);
                foreach (var entry in group)
                {
                    builder.Append(entry.Keys)
                        .Append(" — ")
                        .AppendLine(entry.Action);
                }
            }

            builder.AppendLine();
            builder.Append("输入 /help 查看 Slash 命令目录。");
            return builder.ToString();
        }
    }
}
