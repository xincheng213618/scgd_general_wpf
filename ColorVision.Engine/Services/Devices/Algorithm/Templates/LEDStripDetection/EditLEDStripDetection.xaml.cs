﻿using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI.Comply;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LEDStripDetection
{
    /// <summary>
    /// EditLEDStripDetection.xaml 的交互逻辑
    /// </summary>
    public partial class EditLEDStripDetection : UserControl
    {
        public EditLEDStripDetection()
        {
            InitializeComponent();
        }
        public LEDStripDetectionParam Param { get; set; }

        public void SetParam(LEDStripDetectionParam param)
        {
            ComboBoxValidate.ItemsSource = TemplateComplyParam.Params["Comply.CIE.AVG"]?.CreateEmpty();
            ComboBoxValidateCIE.ItemsSource = TemplateComplyParam.Params["Comply.CIE"]?.CreateEmpty();
            Param = param;
            this.DataContext = Param;
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {

        }
    }
}
