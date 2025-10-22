using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.CompoundImg
{
    /// <summary>
    /// DisplayCompoundImg.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCompoundImg : UserControl
    {
        public AlgorithmCompoundImg IAlgorithm { get; set; }
        public DisplayCompoundImg(AlgorithmCompoundImg iAlgorithm)
        {
            IAlgorithm = iAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;

            ComboxTemplate.ItemsSource = TemplateCompoundImg.Params;
            ComboxTemplate.SelectedIndex = 0;
        }

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsTemplateSelected(ComboxTemplate, "请先选择图像拼接模板")) return;

            if (ComboxTemplate.SelectedValue is not TemplateJsonParam param) return;

            MsgRecord msg = IAlgorithm.SendCommand(param);
            ServicesHelper.SendCommand(sender, msg);
        }
    }
}
