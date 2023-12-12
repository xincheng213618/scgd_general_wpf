using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using ColorVision.Flow.Templates;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using cvColorVision;
using NPOI.SS.Formula.Functions;

namespace ColorVision.Templates
{
    /// <summary>
    /// CalibrationUpload.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationUpload : Window
    {

        public CalibrationUpload()
        {
            InitializeComponent();
            this.DragEnter += (s, e) =>
            {
                e.Effects = DragDropEffects.Scroll;
                e.Handled = true;
                UploadRec.Stroke = Brushes.Blue;
            };
            this.DragLeave += (s, e) =>
            {
                UploadRec.Stroke = Brushes.Gray;
            };
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            CobCalibration.ItemsSource = Enum.GetValues(typeof(CalibrationType)).Cast<CalibrationType>();
            CobCalibration.SelectedIndex = 0;

        }
        private void UIElement_OnDrop(object sender, DragEventArgs e)
        {
            UploadRec.Stroke = Brushes.Gray;
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);

            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                TxtCalibrationFile.Text = a?.First();
                TxtCalibrationFileName.Text = Path.GetFileName(a?.First());
            }
        }

        private void UIElement_OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            openFileDialog.Filter = "All files (*.*)|*.*";
            openFileDialog.Multiselect = false;

            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                TxtCalibrationFile.Text = openFileDialog.FileName;
                TxtCalibrationFileName.Text = openFileDialog.SafeFileName;
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            SysResourceModel sysResourceModel = new SysResourceModel();
            sysResourceModel.Name = TxtCalibrationFileName.Text;
            sysResourceModel.Value = TxtCalibrationFile.Text;
            sysResourceModel.Type = 11;

            if (CobCalibration.SelectedValue is CalibrationType CalibrationType)
            {
                sysResourceModel.Type = (int)CalibrationRsourceService.GetInstance().CalibrationType2ResouceType(CalibrationType);
            }
            
            int ret = CalibrationRsourceService.GetInstance().Save(sysResourceModel);
            if (ret == 1)
                MessageBox.Show("上传成功");
        }
    }
}
