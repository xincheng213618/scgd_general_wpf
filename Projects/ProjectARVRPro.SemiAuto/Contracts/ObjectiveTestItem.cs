using ColorVision.Common.MVVM;

namespace ProjectARVRPro
{
    /// <summary>
    /// 单个客观测试项。所有光学结果最终都会落到这个结构，客户系统通常读取 Value/Unit/LowLimit/UpLimit/TestResult。
    /// </summary>
    public class ObjectiveTestItem : ViewModelBase
    {
        /// <summary>
        /// 测试项名称，来自 ARVRPro 输出配置，用于人读和字段识别。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 格式化后的测试值字符串，可能包含百分号、小数格式或其他显示符号。
        /// </summary>
        public string TestValue { get; set; } = string.Empty;

        /// <summary>
        /// 数值型测试值，建议 MES/上位机用这个字段做判定、存档和统计。
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// 判定下限。为 0 时通常表示不启用下限判定。
        /// </summary>
        public double LowLimit { get; set; }

        /// <summary>
        /// 判定上限。为 0 时通常表示不启用上限判定。
        /// </summary>
        public double UpLimit { get; set; }

        /// <summary>
        /// 单位，例如 degree、cd/m^2、%、K；无量纲参数可为空或 None。
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// 单项判定结果。true 表示 Value 在启用的上下限范围内，false 表示超限。
        /// </summary>
        public bool TestResult
        {
            get
            {
                bool isAboveLowLimit = LowLimit == 0 || Value >= LowLimit;
                bool isBelowUpLimit = UpLimit == 0 || Value <= UpLimit;
                return isAboveLowLimit && isBelowUpLimit;
            }
        }
    }
}
