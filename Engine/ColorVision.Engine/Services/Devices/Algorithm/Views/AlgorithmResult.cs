#pragma warning disable CS8601
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.UI.Sorts;
using MQTTMessageLib.Algorithm;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    public class AlgorithmResult : ViewModelBase, ISortID, ISortBatch, ISortCreateTime, ISortFilePath
    {
        public ObservableCollection<IViewResult> ViewResults { get; set; }

        public ContextMenu ContextMenu { get; set; }
        public RelayCommand ExportCVCIECommand { get; set; }
        public RelayCommand CopyToCommand { get; set; }
        public RelayCommand OpenContainingFolderCommand { get; set; }

        public RelayCommand ExportToPoiCommand { get; set; }

        public AlgorithmResult(AlgResultMasterModel item)
        {
            Id = item.Id;
            Batch = item.BatchCode;
            FilePath = item.ImgFile;
            POITemplateName = item.TName;
            CreateTime = item.CreateDate;
            ResultType = item.ImgFileType;
            ResultCode = item.ResultCode;
            TotalTime = item.TotalTime;
            ResultDesc = item.Result;

            ExportCVCIECommand = new RelayCommand(a => Export(), a => File.Exists(FilePath));
            CopyToCommand = new RelayCommand(a => CopyTo(), a => File.Exists(FilePath));
            ExportToPoiCommand = new RelayCommand(a => ExportToPoi(), a => ViewResults?.ToSpecificViewResults<PoiResultData>().Count != 0);
            OpenContainingFolderCommand = new RelayCommand(a => OpenContainingFolder());

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = "选中", Command = OpenContainingFolderCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "导出", Command = ExportCVCIECommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "导出到POI", Command = ExportToPoiCommand });
        }

        public void OpenContainingFolder()
        {
            PlatformHelper.OpenFolderAndSelectFile(FilePath);
        }


        public void ExportToPoi()
        {
            var list = ViewResults?.ToSpecificViewResults<PoiResultData>();
            if (list ==null )
                return;
            int old = TemplatePoi.Params.Count;
            TemplatePoi templatePoi = new TemplatePoi();
            templatePoi.ExportTemp = new PoiParam() {  Name = templatePoi.NewCreateFileName("poi")};
            templatePoi.ExportTemp.Height = 400;
            templatePoi.ExportTemp.Width = 300;
            templatePoi.ExportTemp.PoiConfig.BackgroundFilePath = FilePath;
            foreach (var item in list)
            {
                PoiPoint poiPoint = new PoiPoint() {
                    Name = item.Name, 
                    PixX = item.Point.PixelX, 
                    PixY = item.Point.PixelY,
                    PixHeight = item.Point.Height,
                    PixWidth = item.Point.Width,    
                    PointType = (RiPointTypes)item.Point.PointType,
                    Id =-1
                };
                templatePoi.ExportTemp.PoiPoints.Add(poiPoint);
            }


            templatePoi.OpenCreate();
            int next = TemplatePoi.Params.Count;
            if (next ==old + 1)
            {
                new EditPoiParam(TemplatePoi.Params[next-1].Value).ShowDialog();
            }

        }

        public void CopyTo()
        {
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

            if (FilePath != null)
            {
                if (!CVFileUtil.IsCIEFile(FilePath))
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "导出仅支持CIE文件", "ColorVision");
                    return;
                }
                ExportCVCIE exportCVCIE = new ExportCVCIE(FilePath);
                exportCVCIE.Owner = Application.Current.GetActiveWindow();
                exportCVCIE.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                exportCVCIE.ShowDialog();
            }

        }





        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public string? Batch { get { return _Batch; } set { _Batch = value; NotifyPropertyChanged(); } }
        private string? _Batch;

        public string? FilePath { get { return _FilePath; } set { _FilePath = value; NotifyPropertyChanged(); } }
        private string? _FilePath;

        public string POITemplateName { get { return _POITemplateName; } set { _POITemplateName = value; NotifyPropertyChanged(); } }
        private string _POITemplateName;

        public DateTime? CreateTime { get { return _CreateTime; } set { _CreateTime = value; NotifyPropertyChanged(); } }
        private DateTime? _CreateTime;

        public AlgorithmResultType ResultType {get=> _ResultType; set { _ResultType = value; NotifyPropertyChanged(); } }
        private AlgorithmResultType _ResultType;

        public string ResultDesc { get { return _ResultDesc; } set { _ResultDesc = value; NotifyPropertyChanged(); } }
        private string _ResultDesc;

        public long TotalTime { get => _TotalTime; set { _TotalTime = value; NotifyPropertyChanged(); } }
        private long _TotalTime;

        public int? ResultCode { get { return _ResultCode; } set { _ResultCode = value; NotifyPropertyChanged(); } }
        private int? _ResultCode;

        public string Result => ResultCode == 0 ? "成功" : "失败";


    }
}
