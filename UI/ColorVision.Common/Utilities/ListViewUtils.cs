using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Common.Utilities
{
    public static class ListViewUtils
    {
        public static void Copy(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ListView myListView)
            {
                var item = myListView.SelectedItem;
                if (item != null)
                {
                    // 获取所有属性
                    var props = item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    StringBuilder sb = new StringBuilder();
                    foreach (var prop in props)
                    {
                        var name = prop.Name;
                        // 跳过属性名包含 ContextMenu 或 Command 的属性
                        if (name.Contains("ContextMenu") || name.Contains("Command"))
                            continue;
                        // 可根据需求过滤属性
                        var value = prop.GetValue(item);
                        sb.Append(value?.ToString());
                        sb.Append("\t"); // 用Tab分隔
                    }
                    NativeMethods.Clipboard.SetText(sb.ToString().TrimEnd('\t'));
                }
            }

        }
    }
}
