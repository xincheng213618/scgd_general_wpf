﻿using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Configs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.RC
{
    public delegate void UseRcServicelHandler(bool IsUseRcServicel);

    public class RCSettingProvider : IConfigSettingProvider,IStatusBarIconProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = "启用RC",
                    Description = "启用RC",
                    Type = ConfigSettingType.Bool,
                    Order =10,
                    BindingName = nameof(RCSetting.IsUseRCService),
                    Source = RCSetting.Instance
                },
                new ConfigSettingMetadata
                {
                    Name = "打开CVWinSMS",
                    Description = "在软件启动时，如果未打开RC,则打开RC",
                    Type = ConfigSettingType.Bool,
                    BindingName = nameof(RCManagerConfig.IsOpenCVWinSMS),
                    Order =11,
                    Source = RCManagerConfig.Instance
                }
            };
        }

        public IEnumerable<StatusBarIconMetadata> GetStatusBarIconMetadata()
        {
            Action action = new Action(() =>
            {
                new RCServiceConnect() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            });

            return new List<StatusBarIconMetadata>
            {
                new StatusBarIconMetadata()
                {
                    Name = "RC",
                    Description = "RC",
                    Order =3,
                    BindingName = "RCServiceControl.IsConnect",
                    VisibilityBindingName =nameof(RCSetting.IsUseRCService),
                    ButtonStyleName ="ButtonDrawingImageRCService",
                    Source = RCSetting.Instance,
                    Action =action
                }
            };
        }
    }


    public class RCSetting : ViewModelBase, IConfigSecure
    {
        public static RCSetting Instance => ConfigHandler.GetInstance().GetRequiredService<RCSetting>();

        public static RCServiceControl RCServiceControl => RCServiceControl.GetInstance();

        public RCServiceConfig RCServiceConfig { get; set; } = new RCServiceConfig();
        public bool IsUseRCService { get => _IsUseRCService; set { _IsUseRCService = value; NotifyPropertyChanged(); } }
        private bool _IsUseRCService = true;

        public ObservableCollection<RCServiceConfig> RCServiceConfigs { get; set; } = new ObservableCollection<RCServiceConfig>();
        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "ColorVision";

        public void Decrypt()
        {
            RCServiceConfig.AppSecret = Cryptography.AESDecrypt(RCServiceConfig.AppSecret, ConfigAESKey, ConfigAESVector);
        }

        public void Encryption()
        {
            RCServiceConfig.AppSecret = Cryptography.AESEncrypt(RCServiceConfig.AppSecret, ConfigAESKey, ConfigAESVector);
        }
    }
}