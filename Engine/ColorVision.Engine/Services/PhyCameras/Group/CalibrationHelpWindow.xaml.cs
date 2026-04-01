using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    /// <summary>
    /// 校正系统参考手册窗口：展示完整的校正流水线说明、算法原理、公式和配置文件格式。
    /// 帮助用户理解各校正步骤的功能和使用方式。
    /// </summary>
    public partial class CalibrationHelpWindow : Window
    {
        private static CalibrationHelpWindow _instance;

        public CalibrationHelpWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        /// <summary>
        /// 单例模式打开帮助窗口，避免重复创建
        /// </summary>
        public static void ShowHelp(Window owner = null)
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new CalibrationHelpWindow();
                if (owner != null)
                    _instance.Owner = owner;
                _instance.Closed += (s, e) => _instance = null;
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }
        }
    }
}
