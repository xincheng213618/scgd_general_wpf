#pragma warning disable CS8604,CS8629,CS8601
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.Themes.Controls;
using ColorVision.UI.Sorts;
using MQTTMessageLib.Camera;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services
{
    public class ViewResultImage : ViewModelBase, ISortID
    {
        public ContextMenu ContextMenu { get; set; }
        public RelayCommand ExportCVCIECommand { get; set; }
        public RelayCommand OpenCVCIECommand { get; set; }
        public RelayCommand CopyToCommand { get; set; }

        public RelayCommand OpenContainingFolderCommand { get; set; }
        public RelayCommand CreateToPoiCommand { get; set; }

        public ViewResultImage()
        {

        }
        public ViewResultImage(MeasureResultImgModel measureImgResultModel)
        {
            Id = measureImgResultModel.Id;
            FilePath = measureImgResultModel.RawFile ?? string.Empty;
            FileUrl = measureImgResultModel.FileUrl ?? string.Empty;
            FileType = (CameraFileType)(measureImgResultModel.FileType ?? 0);
            ReqParams = measureImgResultModel.Params ?? string.Empty;
            ImgFrameInfo = measureImgResultModel.ImgFrameInfo ?? string.Empty;
            CreateTime = measureImgResultModel.CreateDate;
            ResultCode = measureImgResultModel.ResultCode;
            ResultMsg = measureImgResultModel.Result;
            ResultDesc = measureImgResultModel.Result ?? string.Empty;
            _totalTime = measureImgResultModel.TotalTime;
            DeviceCode = measureImgResultModel.DeviceCode;
            ExportCVCIECommand = new RelayCommand(a => Export(), a => File.Exists(FileUrl));
            OpenCVCIECommand = new RelayCommand(a => Open(), a => File.Exists(FileUrl));
            CopyToCommand = new RelayCommand(a => CopyTo(), a => File.Exists(FileUrl));
            CreateToPoiCommand = new RelayCommand(a => CreateToPoi(), a => File.Exists(FileUrl));

            ContextMenu = new ContextMenu();
            OpenContainingFolderCommand = new RelayCommand(a => System.Diagnostics.Process.Start("explorer.exe", $"/select,{FileUrl}"), a => File.Exists(FileUrl));
            ContextMenu.Items.Add(new MenuItem() { Header = "在文件夹中选中文件", Command = OpenContainingFolderCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "导出", Command = ExportCVCIECommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "作为底图创建POI", Command = CreateToPoiCommand });

            Task.Run(() =>
            {
                bool exists = !string.IsNullOrEmpty(FileUrl) && File.Exists(FileUrl);
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    IsFileExists = exists;
                });
            });

        }

        public bool IsFileExists { get => _IsFileExists; set { if (_IsFileExists == value) return; _IsFileExists = value; OnPropertyChanged(); } }
        private bool _IsFileExists = true;

        public void CreateToPoi()
        {
            TemplatePoi templatePoi = new TemplatePoi();
            templatePoi.ImportTemp = new PoiParam() { Name = templatePoi.NewCreateFileName("poi") };
            templatePoi.ImportTemp.Height = 400;
            templatePoi.ImportTemp.Width = 300;
            templatePoi.ImportTemp.PoiConfig.BackgroundFilePath = FileUrl;
            templatePoi.OpenCreate();
        }

        public void CopyTo()
        {
            string FilePath = FileUrl;

            if (File.Exists(FilePath))
            {
                if (CVFileUtil.IsCIEFile(FilePath))
                {
                    int index = CVFileUtil.ReadCIEFileHeader(FilePath, out var meta);
                    if (index > 0)
                    {
                        if (meta.srcFileName != null && !File.Exists(meta.srcFileName))
                        {
                            meta.srcFileName = Path.Combine(Path.GetDirectoryName(FilePath) ?? string.Empty, meta.srcFileName);

                        }
                        else
                        {
                            meta.srcFileName = string.Empty;
                        }
                    }

                    System.Windows.Forms.FolderBrowserDialog dialog = new();
                    dialog.UseDescriptionForTitle = true;
                    dialog.Description = "选择要保存到得位置";
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        if (string.IsNullOrEmpty(dialog.SelectedPath))
                        {
                            MessageBox.Show("文件夹路径不能为空", "提示");
                            return;
                        }
                        string savePath = dialog.SelectedPath;
                        // Copy the file to the new location
                        string newFilePath = Path.Combine(savePath, Path.GetFileName(FilePath));
                        File.Copy(FilePath, newFilePath, true);

                        // If srcFileName exists, copy it to the new location as well
                        if (File.Exists(meta.srcFileName))
                        {
                            string newSrcFilePath = Path.Combine(savePath, Path.GetFileName(meta.srcFileName));
                            File.Copy(meta.srcFileName, newSrcFilePath, true);
                        }
                    }

                }
                else
                {
                    MessageBox1.Show(WindowHelpers.GetActiveWindow(), "目前支持CVRAW图像", "ColorVision");
                }
            }
            else
            {
                MessageBox1.Show(WindowHelpers.GetActiveWindow(), "找不到原始文件", "ColorVision");
            }
        }

        public void Export()
        {
            ExportCVCIE exportCVCIE = new(FileUrl);
            exportCVCIE.Owner = Application.Current.GetActiveWindow();
            exportCVCIE.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            exportCVCIE.ShowDialog();
        }


        public void Open()
        {
            if (File.Exists(FileUrl))
            {
                ImageView imageView = new();
                Window window = new() { Title = Properties.Resources.QuickPreview, Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                window.Content = imageView;
                imageView.OpenImage(FileUrl);
                window.Show();
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    imageView.ImageViewModel.ClearImage();
                }));
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "找不到文件", "ColorVision");
            }
        }

        [DisplayName("SerialNumber1")]
        public int Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private int _Id;

        public string? Batch { get => _Batch; set { _Batch = value; OnPropertyChanged(); } }
        private string? _Batch;

        [DisplayName("FileUrl")]
        public string? FileUrl { get => _FileUrl; set { _FileUrl = value; OnPropertyChanged(); } }
        private string? _FileUrl;

        [DisplayName("File")]
        public string? FilePath { get => _FilePath; set { _FilePath = value; OnPropertyChanged(); } }
        private string? _FilePath;

        public CameraFileType FileType { get => _FileType; set { _FileType = value; OnPropertyChanged(); } }
        private CameraFileType _FileType;
        [DisplayName("Parameter")]
        public string ReqParams { get => _Params; set { _Params = value; OnPropertyChanged(); } }
        private string _Params;
        [DisplayName("ImageInfo")]
        public string ImgFrameInfo { get => _ImgFrameInfo; set { _ImgFrameInfo = value; OnPropertyChanged(); } }
        private string _ImgFrameInfo;

        public DateTime? CreateTime { get => _RecvTime; set { _RecvTime = value; OnPropertyChanged(); } }
        private DateTime? _RecvTime;

        [DisplayName("DeviceCode")]
        public string DeviceCode { get => _DeviceCode; set { _DeviceCode = value; OnPropertyChanged(); } }
        private string _DeviceCode;

        [DisplayName("Info")]
        public string? ResultMsg { get => _ResultMsg; set { _ResultMsg = value; OnPropertyChanged(); } }
        private string? _ResultMsg;
        public int ResultCode { get => _resultCode; set { _resultCode = value; OnPropertyChanged(); } }
        private int _resultCode;
        [DisplayName("Duration")]
        public string TotalTime => string.Format("{0}", TimeSpan.FromMilliseconds(_totalTime).ToString(@"mm\:ss\:fff"));
        private long _totalTime;

        private string _resultDesc;



        public string ResultDesc { get => _resultDesc; set { _resultDesc = value; OnPropertyChanged(); } }
    }


}
