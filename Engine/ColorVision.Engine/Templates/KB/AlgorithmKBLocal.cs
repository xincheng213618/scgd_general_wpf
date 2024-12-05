using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.KB
{
    public class AlgorithmKBLocalConfig : ViewModelBase, IConfig
    {
        public static AlgorithmKBLocalConfig Instance =>ConfigService.Instance.GetRequiredService<AlgorithmKBLocalConfig>();

        public string LuminFile { get => _LuminFile; set { _LuminFile = value; NotifyPropertyChanged(); } }
        private string _LuminFile;

        public string SaveFolderPath { get => _SaveFolderPath; set { _SaveFolderPath = value; NotifyPropertyChanged(); } }
        private string _SaveFolderPath;

    }

    public class AlgorithmKBLocal : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "KBLocal";
        public int Order { get; set; } = 99;
        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand SelectLuminFileCommand { get; set; }
        public RelayCommand SelcetSaveFilePathCommand { get; set; }

        public AlgorithmKBLocal()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            SelectLuminFileCommand = new RelayCommand(a => SelectLuminFile());
            SelcetSaveFilePathCommand = new RelayCommand(a => SelcetSaveFilePath());
        }

        public AlgorithmKBLocal(DeviceAlgorithm deviceAlgorithm)
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            SelectLuminFileCommand = new RelayCommand(a => SelectLuminFile());
            SelcetSaveFilePathCommand = new RelayCommand(a => SelcetSaveFilePath());
        }

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplatePoi(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        public int HaloThreadV { get => _HaloThreadV; set { _HaloThreadV = value; NotifyPropertyChanged(); } }
        private int _HaloThreadV = 500;
        public int KeyThreadV { get => _KeyThreadV; set { _KeyThreadV = value; NotifyPropertyChanged(); } }
        private int _KeyThreadV = 3000;
        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public string LuminFile { get => AlgorithmKBLocalConfig.Instance.LuminFile; set { AlgorithmKBLocalConfig.Instance.LuminFile = value; NotifyPropertyChanged(); } }

        public void SelectLuminFile()
        {
            using (System.Windows.Forms.OpenFileDialog saveFileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                saveFileDialog.Filter = "标定文件 (*.dat)|*.dat";
                saveFileDialog.Title = "选择标定文件";
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    LuminFile = saveFileDialog.FileName;
                }
            }
        }
        public string SaveFolderPath { get => AlgorithmKBLocalConfig.Instance.SaveFolderPath; set { AlgorithmKBLocalConfig.Instance.SaveFolderPath = value; NotifyPropertyChanged(); } }

        public void SelcetSaveFilePath()
        {
            using (System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                folderBrowserDialog.Description = "Select Folder";
                folderBrowserDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SaveFolderPath = folderBrowserDialog.SelectedPath;
                }
            }
        }

        public int SaveProcessData { get => _saveProcessData; set { _saveProcessData = value; NotifyPropertyChanged(); } }
        private int _saveProcessData = 1;

        public int Exp { get => _Exp; set { _Exp = value; NotifyPropertyChanged(); } }
        private int _Exp = 100;



        public UserControl GetUserControl()
        {
            UserControl ??= new DisplayKB(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }

    }
}
