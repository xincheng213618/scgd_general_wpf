using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
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
        public CalibrationResource(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            CalibrationResources.Add(this);
            OpenCommand = new RelayCommand(a=> Open(),a => AccessControl.Check(PermissionMode.Administrator));
            EditCommand = new RelayCommand(a => Edit(), a => AccessControl.Check(PermissionMode.Administrator));
            Config = JsonConvert.DeserializeObject<CalibrationFileConfig>(sysResourceModel.Remark) ?? new CalibrationFileConfig();
        }

        public void Edit()
        {

            if (this.GetAncestor<PhyCamera>() is PhyCamera phyCamera)
            {
                log.Info(phyCamera.Config.FileServerCfg.FileBasePath);

                if (Directory.Exists(phyCamera.Config.FileServerCfg.FileBasePath))
                {
                    string path = SysResourceModel.Value ?? string.Empty;

                    string filepath = Path.Combine(phyCamera.Config.FileServerCfg.FileBasePath, phyCamera.Code, "cfg", path);
                    log.Info(filepath);
                    AvalonEditWindow avalonEditWindow = new AvalonEditWindow(filepath) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                    avalonEditWindow.ShowDialog();
                }
                else
                {
                    log.Info($"找不到phyCamera.Config.FileServerCfg.FileBasePath{phyCamera.Config.FileServerCfg.FileBasePath}");
                }
            }
            else
            {
                log.Info("找不到物理相机");
            }
        }


        public void Open()
        {
            if (this.GetAncestor<PhyCamera>() is PhyCamera phyCamera)
            {
                if (Directory.Exists(phyCamera.Config.FileServerCfg.FileBasePath))
                {
                    string path = SysResourceModel.Value ?? string.Empty;

                    string filepath = Path.Combine(phyCamera.Config.FileServerCfg.FileBasePath, phyCamera.Code,"cfg", path);

                    PlatformHelper.OpenFolderAndSelectFile(filepath);
                }
            }
        }


        public override void Save()
        {
            SysResourceModel.Remark = JsonConvert.SerializeObject(Config);
            VSysResourceDao.Instance.Save(SysResourceModel);
        }
    }
}
