using ColorVision.Engine.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.PG
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoPG : UserControl
    {
        public DevicePG DevicePG { get; set; }
        public bool IsCanEdit { get; set; }
        public InfoPG(DevicePG devicePG, bool isCanEdit = true)
        {
            DevicePG = devicePG;
            IsCanEdit = isCanEdit;
            InitializeComponent();


        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            DataContext = DevicePG;
        }


    }
}
