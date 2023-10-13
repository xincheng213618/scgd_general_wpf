using ColorVision.Device.Algorithm;
using ColorVision.MySql.Service;
using ColorVision.Services;
using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.POI
{
    /// <summary>
    /// AlgorithmDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmDisplayControl : UserControl
    {
        public DeviceAlgorithm Device { get; set; }

        public AlgorithmService Service { get => Device.Service; }

        public AlgorithmView View { get => Device.View; }


        public AlgorithmDisplayControl(DeviceAlgorithm device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ComboxPoiTemplate.ItemsSource = TemplateControl.GetInstance().PoiParams;
            ComboxPoiTemplate.SelectedIndex = 0;

            ComboxMTFTemplate.ItemsSource = TemplateControl.GetInstance().MTFParams;
            ComboxMTFTemplate.SelectedIndex = 0;

            ComboxSFRTemplate.ItemsSource = TemplateControl.GetInstance().SFRParams;
            ComboxSFRTemplate.SelectedIndex = 0;

            ComboxGhostTemplate.ItemsSource = TemplateControl.GetInstance().GhostParams;
            ComboxGhostTemplate.SelectedIndex = 0;

            ComboxFOVTemplate.ItemsSource = TemplateControl.GetInstance().FOVParams;
            ComboxFOVTemplate.SelectedIndex = 0;

            ComboxDistortionTemplate.ItemsSource = TemplateControl.GetInstance().DistortionParams;
            ComboxDistortionTemplate.SelectedIndex = 0;


            ViewGridManager.GetInstance().AddView(Device.View);
            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) =>
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2));
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1));
                for (int i = 0; i < e; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = View.View.ViewIndex;
            };
            View.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                ComboxView.SelectedIndex = e2 + 2;
            };
            ComboxView.SelectionChanged += (s, e) =>
            {
                if (ComboxView.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    View.View.ViewIndex = KeyValue.Value;
                    ViewGridManager.GetInstance().SetViewIndex(View, KeyValue.Value);
                }
            };
            View.View.ViewIndex = -1;


        }

        private void PoiClick(object sender, RoutedEventArgs e)
        {
            if (ComboxPoiTemplate.SelectedIndex ==-1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var Batch = ServiceControl.GetInstance().GetResultBatch(sn);
            Service.GetData(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, Batch.Id);
        }

        private void Algorithm_INI(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                 var msg = Service.Init();

                Helpers.SendCommand(button, msg);

            }
        }

        private void Algorithm_GET(object sender, RoutedEventArgs e)
        {
            Service.GetAllSnID();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var a = new ResultService().PoiSelectByBatchID(10);
            Device.View.PoiDataDraw(a);
        }

        private void MTF_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxMTFTemplate.SelectedIndex==-1)
            {
                MessageBox.Show("请先选择MTF模板");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }

            var ss = Service.MTF(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, ImageFile.Text, TemplateControl.GetInstance().MTFParams[ComboxMTFTemplate.SelectedIndex].Value);
            Helpers.SendCommand(ss,"MTF");
        }

        private void SFR_Clik(object sender, RoutedEventArgs e)
        {
            if (ComboxSFRTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择SFR模板");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }

            var msg = Service.SFR(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, ImageFile.Text, TemplateControl.GetInstance().SFRParams[ComboxSFRTemplate.SelectedIndex].Value);
            Helpers.SendCommand(msg, "SFR");

        }

        private void Ghost_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxGhostTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择Ghost模板");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }



            var msg = Service.Ghost(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, ImageFile.Text, TemplateControl.GetInstance().GhostParams[ComboxGhostTemplate.SelectedIndex].Value);
            Helpers.SendCommand(msg, "Ghost");
        }

        private void Distortion_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxDistortionTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择Distortion模板");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }
            var msg = Service.Distortion(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, ImageFile.Text, TemplateControl.GetInstance().DistortionParams[ComboxDistortionTemplate.SelectedIndex].Value);
            Helpers.SendCommand(msg, "Distortion");
        }


        private void FOV_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxFOVTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择FOV模板");
                return;
            }
            if (ComboxPoiTemplate.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择关注点模板");
                return;
            }

            var msg = Service.FOV(TemplateControl.GetInstance().PoiParams[ComboxPoiTemplate.SelectedIndex].Value.ID, ImageFile.Text, TemplateControl.GetInstance().FOVParams[ComboxFOVTemplate.SelectedIndex].Value);
            Helpers.SendCommand(msg, "FOV");
        }


        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.InitialDirectory = Environment.CurrentDirectory;
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }


             
        }


    }
}
