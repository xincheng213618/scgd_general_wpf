using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Services.Type;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Services.PhyCameras
{
    public class PhyCameraManager
    {
        private static PhyCameraManager _instance;
        private static readonly object _locker = new();
        public static PhyCameraManager GetInstance() { lock (_locker) { return _instance ??= new PhyCameraManager(); } }

        public RelayCommand CreateCommand { get; set; }


        public PhyCameraManager() 
        {
            CreateCommand = new RelayCommand(a => Create());
        }

        public void Create()
        {
            CreateWindow createWindow = new CreateWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            createWindow.ShowDialog();
        }

        public ObservableCollection<PhyCamera> PhyCameras { get; set; } = new ObservableCollection<PhyCamera>();

        public void LoadPhyCamera()
        {
            var list = SysResourceDao.Instance.GetAllType((int)ServiceTypes.PhyCamera);
            foreach (var item in list)
            {
                PhyCameras.Add(new PhyCamera(item));
            }
        }
    }
}
