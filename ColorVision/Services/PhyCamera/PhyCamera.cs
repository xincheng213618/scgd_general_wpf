using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.PhyCameras.Configs;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.PhyCameras
{
    public class PhyCamera : BaseResource
    {
        public ConfigPhyCamera Config { get; set; }

        public RelayCommand UploadCalibrationCommand { get; set; }
        public RelayCommand EditCommand { get; set; }
        public ContextMenu ContextMenu { get; set; }

        public PhyCamera(SysResourceModel sysResourceModel):base(sysResourceModel)
        {
            Config = BaseResourceObjectExtensions.TryDeserializeConfig<ConfigPhyCamera>(SysResourceModel.Value);
            DeleteCommand = new RelayCommand(a => Delete());
            EditCommand = new RelayCommand(a =>
            {
                EditPhyCamera window = new EditPhyCamera(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });
            ContentInit();
        }

        public override void Delete()
        {
            base.Delete();
            PhyCameraManager.GetInstance().PhyCameras.Remove(this);
        }

        public void ContentInit()
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Edit, Command = EditCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Delete, Command = DeleteCommand });

        }

        public void SaveConfig()
        {
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            SysResourceDao.Instance.Save(SysResourceModel);
        }
        public override void Save()
        {
            base.Save();
            SaveConfig();
        }

    }
}
