#pragma warning disable CS8603
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Types;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.PhySpectrums
{

    public class PhySpectrum : ServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PhySpectrum));
        public PhySpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {

        }

        public UserControl UserControl { get; set; }

        public UserControl GetDeviceInfo()
        {
            if (UserControl != null && UserControl.Parent is Grid grid)
            {
                grid.Children.Remove(UserControl);
            }
            return UserControl;
        }
    }

    public class PhySpectrumManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PhySpectrumManager));
        private static PhySpectrumManager _instance;
        private static readonly object Locker = new();
        public static PhySpectrumManager GetInstance() { lock (Locker) { return _instance ??= new PhySpectrumManager(); } }

        public ObservableCollection<PhySpectrum> PhySpectrums { get; set; }

        public EventHandler Loaded { get; set; }


        public PhySpectrumManager()
        {
            PhySpectrums = new ObservableCollection<PhySpectrum>();

            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => Load();
            if (MySqlControl.GetInstance().IsConnect)
                Load();

            RefreshEmpty();
            PhySpectrums.CollectionChanged += (s, e) => RefreshEmpty();

        }
        public void Load()
        {
            var phySpectrumBackup = PhySpectrums.ToDictionary(ps => ps.Id, ps => ps);

            var list = SysResourceDao.Instance.GetAllType((int)ServiceTypes.PhyCamera);
            foreach (var item in list)
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    // 创建新的 PhyCamera 对象

                    // 如果备份字典中存在该 PhyCamera 的 Id
                    if (phySpectrumBackup.TryGetValue(item.Id, out var existingPhyCamera))
                    {
                        existingPhyCamera.Name = item.Name ?? string.Empty;
                        existingPhyCamera.SysResourceModel = item;
                    }
                    else
                    {
                        var newPhyCamera = new PhySpectrum(item);
                        PhySpectrums.Add(newPhyCamera);
                    }
                }
            }

            Loaded?.Invoke(this, EventArgs.Empty);
        }

        public void RefreshEmpty()
        {
            Count = SysResourceDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "type", 103 } }).Where(a => string.IsNullOrWhiteSpace(a.Value)).ToList().Count;
        }


    public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
    private int _Count;


    }
}
