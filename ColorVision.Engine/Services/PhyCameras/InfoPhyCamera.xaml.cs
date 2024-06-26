﻿using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Engine.Services.PhyCameras
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoPhyCamera : UserControl
    {
        public PhyCamera Device { get; set; }

        public bool IsCanEdit { get; set; }
        public InfoPhyCamera(PhyCamera deviceCamera,bool isCanEdit =true)
        {
            Device = deviceCamera;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            DataContext = Device;
        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show("数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }            
            var ITemplate = new TemplateCalibrationParam(Device) ;
            new WindowTemplate(ITemplate) { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }

        private void TextBlock_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                Common.NativeMethods.Clipboard.SetText(textBlock.Text);
                MessageBox.Show(textBlock.Text);
            }
        }

        private void UniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UniformGrid uniformGrid)
            {
                uniformGrid.Columns = uniformGrid.ActualWidth > 0 ? (int)(uniformGrid.ActualWidth / 200) : 1;
                uniformGrid.Rows = (int)Math.Ceiling(uniformGrid.Children.Count / (double)uniformGrid.Columns);
            }
        }
    }
}
