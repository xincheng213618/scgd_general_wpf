using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.UI.Examples
{
    /// <summary>
    /// 示例：如何使用增强的 ListNumericJsonEditor
    /// </summary>
    public class ListEditorExample
    {
        // 测试枚举
        public enum Priority
        {
            [Description("低")]
            Low,
            [Description("中")]
            Medium,
            [Description("高")]
            High,
            [Description("紧急")]
            Critical
        }

        public enum Status
        {
            Pending,
            InProgress,
            Completed,
            Cancelled
        }

        /// <summary>
        /// 示例配置类，展示不同类型的列表属性
        /// </summary>
        [DisplayName("列表编辑器示例配置")]
        public class SampleConfig
        {
            [Category("数值列表")]
            [DisplayName("整数列表")]
            [Description("点击'编辑'按钮可以可视化编辑列表项")]
            public List<int> IntegerList { get; set; } = new List<int> { 1, 2, 3, 4, 5 };

            [Category("数值列表")]
            [DisplayName("浮点数列表")]
            [Description("支持小数输入和验证")]
            public List<double> DoubleList { get; set; } = new List<double> { 1.1, 2.2, 3.3 };

            [Category("文本列表")]
            [DisplayName("字符串列表")]
            [Description("编辑时可以选择文件或文件夹")]
            public List<string> StringList { get; set; } = new List<string> 
            { 
                "Apple", 
                "Banana", 
                "Cherry" 
            };

            [Category("文本列表")]
            [DisplayName("文件路径列表")]
            [Description("用于存储文件路径，编辑时提供文件选择器")]
            public List<string> FilePaths { get; set; } = new List<string>
            {
                @"C:\Users\Public\sample1.txt",
                @"C:\Users\Public\sample2.txt"
            };

            [Category("枚举列表")]
            [DisplayName("优先级列表")]
            [Description("通过下拉框选择枚举值")]
            public List<Priority> PriorityList { get; set; } = new List<Priority>
            {
                Priority.High,
                Priority.Medium,
                Priority.Low
            };

            [Category("枚举列表")]
            [DisplayName("状态列表")]
            [Description("支持任意枚举类型")]
            public List<Status> StatusList { get; set; } = new List<Status>
            {
                Status.Pending,
                Status.InProgress
            };

            [Category("混合示例")]
            [DisplayName("字节列表")]
            [Description("支持所有数值类型")]
            public List<byte> ByteList { get; set; } = new List<byte> { 0, 128, 255 };

            [Category("混合示例")]
            [DisplayName("长整数列表")]
            [Description("支持大数值")]
            public List<long> LongList { get; set; } = new List<long> { 1000000L, 2000000L };

            [Category("嵌套列表")]
            [DisplayName("整数矩阵")]
            [Description("嵌套列表：List<List<int>>，支持二级编辑")]
            public List<List<int>> IntegerMatrix { get; set; } = new List<List<int>>
            {
                new List<int> { 1, 2, 3 },
                new List<int> { 4, 5, 6 },
                new List<int> { 7, 8, 9 }
            };

            [Category("嵌套列表")]
            [DisplayName("字符串分组")]
            [Description("嵌套列表：List<List<string>>，用于分组数据")]
            public List<List<string>> StringGroups { get; set; } = new List<List<string>>
            {
                new List<string> { "Group1-A", "Group1-B", "Group1-C" },
                new List<string> { "Group2-A", "Group2-B" }
            };

            [Category("嵌套列表")]
            [DisplayName("浮点数矩阵")]
            [Description("嵌套列表：List<List<double>>，用于数学计算")]
            public List<List<double>> DoubleMatrix { get; set; } = new List<List<double>>
            {
                new List<double> { 1.1, 2.2, 3.3 },
                new List<double> { 4.4, 5.5, 6.6 }
            };
        }

        /// <summary>
        /// 示例：在代码中使用 PropertyEditorWindow
        /// </summary>
        public static void ShowExample()
        {
            var config = new SampleConfig();
            
            // 创建并显示属性编辑窗口
            var window = new PropertyEditorWindow(config, isEdit: true);
            
            // 用户可以：
            // 1. 直接在 JSON 文本框中编辑（原有方式）
            // 2. 点击"编辑"按钮打开可视化编辑窗口
            //    - 添加新项
            //    - 编辑现有项
            //    - 删除项
            //    - 调整顺序（上移/下移）
            
            if (window.ShowDialog() == true)
            {
                // 用户点击确定后，config 对象的列表属性已更新
                System.Console.WriteLine($"整数列表项数: {config.IntegerList.Count}");
                System.Console.WriteLine($"优先级列表项数: {config.PriorityList.Count}");
            }
        }

        /// <summary>
        /// 示例：编程方式直接使用 ListEditorWindow
        /// </summary>
        public static void ShowListEditorDirectly()
        {
            var numbers = new List<int> { 1, 2, 3 };
            
            // 直接创建列表编辑器窗口
            var editor = new PropertyEditor.Editor.List.ListEditorWindow(numbers, typeof(int));
            
            if (editor.ShowDialog() == true)
            {
                // numbers 列表已更新
                System.Console.WriteLine($"更新后的列表: {string.Join(", ", numbers)}");
            }
        }

        /// <summary>
        /// 示例：编辑单个列表项
        /// </summary>
        public static void ShowItemEditorDirectly()
        {
            // 编辑整数
            var intEditor = new PropertyEditor.Editor.List.ListItemEditorWindow(typeof(int), 42);
            if (intEditor.ShowDialog() == true)
            {
                System.Console.WriteLine($"新值: {intEditor.EditedValue}");
            }

            // 编辑字符串（带文件/文件夹选择器）
            var stringEditor = new PropertyEditor.Editor.List.ListItemEditorWindow(typeof(string), "");
            if (stringEditor.ShowDialog() == true)
            {
                System.Console.WriteLine($"新字符串: {stringEditor.EditedValue}");
            }

            // 编辑枚举
            var enumEditor = new PropertyEditor.Editor.List.ListItemEditorWindow(typeof(Priority), Priority.Medium);
            if (enumEditor.ShowDialog() == true)
            {
                System.Console.WriteLine($"新优先级: {enumEditor.EditedValue}");
            }
        }

        /// <summary>
        /// 示例：编辑嵌套列表 (List&lt;List&lt;T&gt;&gt;)
        /// </summary>
        public static void ShowNestedListEditing()
        {
            // 创建一个整数矩阵 (List<List<int>>)
            var matrix = new List<List<int>>
            {
                new List<int> { 1, 2, 3 },
                new List<int> { 4, 5, 6 },
                new List<int> { 7, 8, 9 }
            };

            // 打开外层列表编辑器
            var outerEditor = new PropertyEditor.Editor.List.ListEditorWindow(matrix, typeof(List<int>));
            
            if (outerEditor.ShowDialog() == true)
            {
                // 用户可以在外层编辑器中：
                // 1. 添加新的内层列表（会自动打开嵌套编辑器）
                // 2. 编辑现有的内层列表（会打开嵌套编辑器）
                // 3. 删除整行
                // 4. 调整行的顺序
                
                System.Console.WriteLine("矩阵已更新:");
                foreach (var row in matrix)
                {
                    System.Console.WriteLine($"  [{string.Join(", ", row)}]");
                }
            }

            // 也可以在配置类中使用嵌套列表
            var config = new SampleConfig();
            var window = new PropertyEditorWindow(config, isEdit: true);
            
            if (window.ShowDialog() == true)
            {
                // config.IntegerMatrix 已更新
                System.Console.WriteLine($"整数矩阵行数: {config.IntegerMatrix.Count}");
                System.Console.WriteLine($"字符串分组数: {config.StringGroups.Count}");
            }
        }
    }
}
