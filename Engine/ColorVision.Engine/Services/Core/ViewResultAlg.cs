#pragma warning disable CS8601
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.Themes.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services
{



    public class ViewResultAlg : ViewModelBase
    {
        public ObservableCollection<IViewResult> ViewResults { get; set; }

        public ContextMenu ContextMenu { get; set; }
        public RelayCommand ExportCVCIECommand { get; set; }
        public RelayCommand CopyToCommand { get; set; }
        public RelayCommand OpenContainingFolderCommand { get; set; }

        public RelayCommand ExportToPoiCommand { get; set; }

        public ViewResultAlg()
        {

        }
        public AlgResultMasterModel AlgResultMasterModel { get; set; }
        public ViewResultAlg(AlgResultMasterModel item)
        {
            AlgResultMasterModel = item;
            Id = item.Id;
            Batch = BatchResultMasterDao.Instance.GetById(item.BatchId)?.Code;
            FilePath = item.ImgFile;
            POITemplateName = item.TName;
            CreateTime = item.CreateDate;
            ResultType = item.ImgFileType;
            ResultCode = item.ResultCode;
            TotalTime = item.TotalTime;
            ResultDesc = item.Result;
            ResultImagFile = item.ResultImagFile;
            Version = item.version;

            ExportCVCIECommand = new RelayCommand(a => Export(), a => File.Exists(FilePath));
            CopyToCommand = new RelayCommand(a => CopyTo(), a => File.Exists(FilePath));
            OpenContainingFolderCommand = new RelayCommand(a => OpenContainingFolder());

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Selected, Command = OpenContainingFolderCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Export, Command = ExportCVCIECommand });
            ExportToPoiCommand = new RelayCommand(a => ExportToPoi(), a => ViewResults?.ToSpecificViewResults<PoiResultData>().Count != 0 || ViewResults?.ToSpecificViewResults<PoiPointResultModel>().Count != 0);
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.CreateToPOI, Command = ExportToPoiCommand });
            Task.Run(() =>
            {
                bool exists = !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);
                if (!exists)
                {
                    //var parts = FilePath.Split([';'], StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
                    //if (parts.Length >= 2)
                    //{
                    //    exists = true;
                    //}
                }

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    IsFileExists = exists;
                });
            });

        }





        public bool IsFileExists { get => _IsFileExists; set { if (_IsFileExists == value) return; _IsFileExists = value; OnPropertyChanged(); } }
        private bool _IsFileExists = true;

        public string? Version { get; set; }
        public void OpenContainingFolder()
        {
            PlatformHelper.OpenFolderAndSelectFile(FilePath);
        }


        public void ExportToPoi()
        {
            var list = ViewResults?.ToSpecificViewResults<PoiResultData>();
            if (list == null) return;
            if (list.Count ==0 )
            {
                var list1 = ViewResults?.ToSpecificViewResults<PoiPointResultModel>();
                if (list1 == null)
                    return;

                int old1 = TemplatePoi.Params.Count;
                TemplatePoi templatePoi1 = new TemplatePoi();
                templatePoi1.ImportTemp = new PoiParam() { Name = templatePoi1.NewCreateFileName("poi") };
                templatePoi1.ImportTemp.Height = 400;
                templatePoi1.ImportTemp.Width = 300;
                templatePoi1.ImportTemp.PoiConfig.BackgroundFilePath = FilePath;
                foreach (var item in list1)
                {
                    PoiPoint poiPoint = new PoiPoint()
                    {
                        Name = item.PoiName,
                        PixX = (double)item.PoiX,
                        PixY = (double)item.PoiY,
                        PixHeight = (double)item.PoiHeight,
                        PixWidth = (double)item.PoiWidth,
                        PointType = (GraphicTypes)item.PoiType,
                        Id = -1
                    };
                    templatePoi1.ImportTemp.PoiPoints.Add(poiPoint);
                }


                templatePoi1.OpenCreate();
                int next1 = TemplatePoi.Params.Count;
                if (next1 == old1 + 1)
                {
                    new EditPoiParam(TemplatePoi.Params[next1 - 1].Value).ShowDialog();
                }
                return;
            }


            int old = TemplatePoi.Params.Count;
            TemplatePoi templatePoi = new TemplatePoi();
            templatePoi.ImportTemp = new PoiParam() {  Name = templatePoi.NewCreateFileName("poi")};
            templatePoi.ImportTemp.Height = 400;
            templatePoi.ImportTemp.Width = 300;
            templatePoi.ImportTemp.PoiConfig.BackgroundFilePath = FilePath;
            foreach (var item in list)
            {
                PoiPoint poiPoint = new PoiPoint() {
                    Name = item.Name, 
                    PixX = item.Point.PixelX, 
                    PixY = item.Point.PixelY,
                    PixHeight = item.Point.Height,
                    PixWidth = item.Point.Width,    
                    PointType = (GraphicTypes)item.Point.PointType,
                    Id =-1
                };
                templatePoi.ImportTemp.PoiPoints.Add(poiPoint);
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
                        if (meta.SrcFileName != null && !File.Exists(meta.SrcFileName))
                        {
                            meta.SrcFileName = Path.Combine(Path.GetDirectoryName(FilePath) ?? string.Empty, meta.SrcFileName);

                        }
                        else
                        {
                            meta.SrcFileName = string.Empty;
                        }
                    }

                    System.Windows.Forms.FolderBrowserDialog dialog = new();
                    dialog.UseDescriptionForTitle = true;
                    dialog.Description = ColorVision.Engine.Properties.Resources.SelectSaveLocation;
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        if (string.IsNullOrEmpty(dialog.SelectedPath))
                        {
                            MessageBox.Show(ColorVision.Engine.Properties.Resources.FolderPathCannotBeEmpty, ColorVision.Engine.Properties.Resources.Hint);
                            return;
                        }
                        string savePath = dialog.SelectedPath;
                        // Copy the file to the new location
                        string newFilePath = Path.Combine(savePath, Path.GetFileName(FilePath));
                        File.Copy(FilePath, newFilePath, true);

                        // If SrcFileName exists, copy it to the new location as well
                        if (File.Exists(meta.SrcFileName))
                        {
                            string newSrcFilePath = Path.Combine(savePath, Path.GetFileName(meta.SrcFileName));
                            File.Copy(meta.SrcFileName, newSrcFilePath, true);
                        }
                    }

                }
                else
                {
                    MessageBox1.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.CurrentlySupportsCvRawImages, "ColorVision");
                }
            }
            else
            {
                MessageBox1.Show(WindowHelpers.GetActiveWindow(),ColorVision.Engine.Properties.Resources.OriginalFileNotFound, "ColorVision");
            }
        }


        public void Export()
        {

            if (FilePath != null)
            {
                if (!CVFileUtil.IsCIEFile(FilePath))
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExportSupportsCieFilesOnly, "ColorVision");
                    return;
                }
                ExportCVCIE exportCVCIE = new ExportCVCIE(FilePath);
                exportCVCIE.Owner = Application.Current.GetActiveWindow();
                exportCVCIE.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                exportCVCIE.ShowDialog();
            }

        }




        [DisplayName("SerialNumber1")]
        public int Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private int _Id;
        [DisplayName("BatchNumber")]
        public string? Batch { get { return _Batch; } set { _Batch = value; OnPropertyChanged(); } }
        private string? _Batch;
        [DisplayName("File")]
        public string? FilePath { get { return _FilePath; } set { _FilePath = value; OnPropertyChanged(); } }
        private string? _FilePath;
        [DisplayName("Template")]
        public string POITemplateName { get { return _POITemplateName; } set { _POITemplateName = value; OnPropertyChanged(); } }
        private string _POITemplateName;
        [DisplayName("CreateTime")]
        public DateTime? CreateTime { get { return _CreateTime; } set { _CreateTime = value; OnPropertyChanged(); } }
        private DateTime? _CreateTime;

        [DisplayName("ResultType")]
        public ViewResultAlgType ResultType {get=> _ResultType; set { _ResultType = value; OnPropertyChanged(); } }
        private ViewResultAlgType _ResultType;

        [DisplayName("ResultDesc")]
        public string ResultDesc { get { return _ResultDesc; } set { _ResultDesc = value; OnPropertyChanged(); } }
        private string _ResultDesc;
        [DisplayName("img_result")]
        public string ResultImagFile { get; set; }
        [DisplayName("Duration")]
        public long TotalTime { get => _TotalTime; set { _TotalTime = value; OnPropertyChanged(); } }
        private long _TotalTime;

        public int? ResultCode { get { return _ResultCode; } set { _ResultCode = value; OnPropertyChanged(); } }
        private int? _ResultCode;


    }
}
