using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Types;
using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.UI.Authorizations;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    public class CalibrationFileConfig : ViewModelBase
    {
        public CalibrationType CalibrationType { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }

        public double Gain { get; set; }

        public double Aperturein { get; set; }
        public double ExpTime { get; set; }
        public double ND { get; set; }
        public double ShotType { get; set; }

        public double Focallength { get; set; }
        public double GetImgMode { get; set; }

        public double ImgBpp { get; set; }
    }



    public class CalibrationResource : ServiceFileBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PhyCamera));

        public static List<CalibrationResource> CalibrationResources { get; set; } = new List<CalibrationResource>();

        public static CalibrationResource EnsureInstance(SysResourceModel sysResourceModel)
        {
            var list = CalibrationResources.Find(a => a.SysResourceModel.Id == sysResourceModel.Id);
            if (list != null)
                return list;
            return new CalibrationResource(sysResourceModel);
        }
        public RelayCommand OpenCommand { get; set; }

        public RelayCommand EditCommand { get; set; }

        public CalibrationFileConfig Config { get; set; }

        public bool IsValid
        {
            get
            {
                return TryGetFilePath(out _);
            }
        }

        public bool CanEditText => SupportsTextEditing((ServiceTypes)SysResourceModel.Type) && IsValid;

        internal static bool SupportsTextEditing(ServiceTypes type) => type is
            ServiceTypes.DarkNoise or ServiceTypes.ColorShift or ServiceTypes.Distortion or
            ServiceTypes.ColorDiff or ServiceTypes.AngleShift or ServiceTypes.Luminance or
            ServiceTypes.LumOneColor or ServiceTypes.LumFourColor or ServiceTypes.LumMultiColor;

        public bool TryGetFilePath(out string filePath)
        {
            filePath = string.Empty;
            if (this.GetAncestor<PhyCamera>() is not PhyCamera phyCamera
                || !Directory.Exists(phyCamera.Config.FileServerCfg.FileBasePath))
            {
                return false;
            }

            string path = SysResourceModel.Value ?? string.Empty;
            filePath = Path.Combine(phyCamera.Config.FileServerCfg.FileBasePath, phyCamera.Code, "cfg", path);
            return File.Exists(filePath);
        }

        public CalibrationResource(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            CalibrationResources.Add(this);
            OpenCommand = new RelayCommand(a=> Open(),a => AccessControl.Check(PermissionMode.Administrator));
            EditCommand = new RelayCommand(a => Edit(), a => CanEditText && AccessControl.Check(PermissionMode.Administrator));
            Config = JsonConvert.DeserializeObject<CalibrationFileConfig>(sysResourceModel.Remark ?? string.Empty) ?? new CalibrationFileConfig();
        }

        public void Edit()
        {
            if (!SupportsTextEditing((ServiceTypes)SysResourceModel.Type))
                return;

            if (TryGetFilePath(out string filepath))
            {
                log.Info(filepath);
                AvalonEditWindow avalonEditWindow = new AvalonEditWindow(filepath) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                avalonEditWindow.ShowDialog();
            }
            else
            {
                log.Info("找不到校正文件");
            }
        }


        public void Open()
        {
            if (TryGetFilePath(out string filepath))
            {
                PlatformHelper.OpenFolderAndSelectFile(filepath);
            }
        }


        public override void Save()
        {
            SysResourceModel.Remark = JsonConvert.SerializeObject(Config);
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, });
            Db.Updateable(SysResourceModel).ExecuteCommand();
        }
    }
}
