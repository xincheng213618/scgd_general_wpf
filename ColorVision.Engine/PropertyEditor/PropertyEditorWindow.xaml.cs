using ColorVision.Common.MVVM;
using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.Engine.PropertyEditor
{
    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class PropertyEditorWindow : Window
    {
        public ViewModelBase Config { get; set; }
        public PropertyEditorWindow(ViewModelBase config)
        {
            Config = config;
            InitializeComponent();
            this.ApplyCaption();
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            PropertyGrid1.SelectedObject = Config;
        }
    }
}
