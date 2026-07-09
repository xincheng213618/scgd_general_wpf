#pragma warning disable CA1816,CA1822,CS0168,CS8602,CS8604,CS8629
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Views;
using ColorVision.Engine.Services;
using ColorVision.Database;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    /// <summary>
    /// DisplayAlgorithm.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayThirdPartyAlgorithms : UserControl,IDisPlayControl,IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayThirdPartyAlgorithms));

        public DeviceThirdPartyAlgorithms Device { get; set; }

        public MQTTThirdPartyAlgorithms DService { get => Device.DService; }


        public ThirdPartyAlgorithmsView View { get => Device.View; }

        public string DisPlayName => Device.Config.Name;

        public DisplayThirdPartyAlgorithms(DeviceThirdPartyAlgorithms device)
        {
            Device = device;
            InitializeComponent();

        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;

            this.ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Property, Command = Device.PropertyCommand });

            void ConfigChanged()
            {
                CB_ThirdPartyAlgorithms.ItemsSource = ThirdPartyAlgorithmsDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "pid", Device.DLLModel?.Id } });
                CB_ThirdPartyAlgorithms.SelectedIndex = 0;
            }
            Device.ConfigChanged += (s, e) => ConfigChanged();
            ConfigChanged();

            void ThirdPartyAlgorithmsChanged()
            {
                if (CB_ThirdPartyAlgorithms.SelectedValue is not ThirdPartyAlgorithmsModel model) return;

                CB_Templates.ItemsSource = TemplateThirdParty.Params.GetValue(model.Code);
                CB_Templates.SelectedIndex = 0;
            }
            CB_ThirdPartyAlgorithms.SelectionChanged += (s, e) => ThirdPartyAlgorithmsChanged();
            ThirdPartyAlgorithmsChanged();

            this.AddViewConfig(View, DisPlayName);
            this.ApplyChangedSelectedColor(DisPlayBorder);
            DService_DeviceStatusChanged(sender, Device.DService.DeviceStatus);
            Device.DService.DeviceStatusChanged += DService_DeviceStatusChanged;
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }
            void HideAllButtons()
            {
                SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                SetVisibility(StackPanelContent, Visibility.Collapsed);
            }
            // Default state
            HideAllButtons();

            switch (e)
            {
                case DeviceStatusType.Unauthorized:
                    SetVisibility(ButtonUnauthorized, Visibility.Visible);
                    break;
                case DeviceStatusType.Unknown:
                    SetVisibility(TextBlockUnknow, Visibility.Visible);
                    break;
                default:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    break;
            }
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }





        private void TemplateSetting_Click(object sender, RoutedEventArgs e)
        {
            if (CB_ThirdPartyAlgorithms.SelectedValue is not ThirdPartyAlgorithmsModel model) return;

            new TemplateEditorWindow(new TemplateThirdParty(model.Code)){ Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        private void Templates_Click(object sender, RoutedEventArgs e)
        {
            if (CB_Templates.SelectedValue is not TemplateJsonParam findDotsArrayParam) return;
            if (!TryGetImageInput(out string imgFileName, out FileExtType fileExtType)) return;

            string type = string.Empty;
            string code = string.Empty;
            DService.CallFunction(findDotsArrayParam, imgFileName, fileExtType, code, type);
        }


        private bool TryGetImageInput(out string imgFileName, out FileExtType fileExtType)
        {
            fileExtType = FileExtType.Tif;
            imgFileName = ImageFile.Text;

            if (string.IsNullOrWhiteSpace(imgFileName))
            {
                MessageBox1.Show(Application.Current.MainWindow, "图像文件不能为空，请先选择图像文件", "ColorVision");
                return false;
            }

            fileExtType = ServicesHelper.ResolveFileExtType(imgFileName);
            return true;
        }




        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = ServicesHelper.ImageFileDialogFilter;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }


        public void Dispose()
        {
            Device.DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
        }
    }
}
