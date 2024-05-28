using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Configs;
using System;
using System.Collections.Generic;

namespace ColorVision.Solution
{

    public class SolutionProvider : IConfigSettingProvider, IStatusBarIconProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {

            };
        }

        public IEnumerable<StatusBarIconMetadata> GetStatusBarIconMetadata()
        {
            Action action = new Action(() => {  });
             
            return new List<StatusBarIconMetadata>
            {
                new StatusBarIconMetadata()
                {
                    Name = "IsLackWarning",
                    Description = "IsLackWarning",
                    Order =4,
                    BindingName = nameof(SolutionSetting.IsLackWarning),
                    VisibilityBindingName = nameof(SolutionSetting.IsShowLackWarning),
                    ButtonStyleName ="ButtonDrawingImageHardDisk",
                    Source = SolutionSetting.Instance,
                    Action =action
                }
            };
        }
    }


    public class SolutionSetting: ViewModelBase,IConfig
    {
        public static SolutionSetting Instance => ConfigHandler.GetInstance().GetRequiredService<SolutionSetting>();

        public string DefaultCreatName { get => _DefaultCreatName; set { _DefaultCreatName = value; NotifyPropertyChanged(); } }
        private string _DefaultCreatName = "新建工程";
         
        public string DefaultSaveName { get => _DefaultSaveName; set { _DefaultSaveName = value; NotifyPropertyChanged(); } }
        private string _DefaultSaveName = "yyyy/dd/MM HH:mm:ss";

        public string DefaultImageSaveName { get => _DefaultImageSaveName; set { _DefaultImageSaveName = value; NotifyPropertyChanged(); } }
        private string _DefaultImageSaveName = "yyyyddMMHHmmss";

        public bool IsMemoryLackWarning{ get => _IsMemoryLackWarning; set { _IsMemoryLackWarning = value; NotifyPropertyChanged(); } }
        private bool _IsMemoryLackWarning = true;

        public bool IsLackWarning { get => _IsLackWarning; set { _IsLackWarning = value; NotifyPropertyChanged(); } }
        private bool _IsLackWarning = true;

        public bool IsShowLackWarning { get => _IsShowLackWarning; set { _IsShowLackWarning = value; NotifyPropertyChanged(); } }
        private bool _IsShowLackWarning = true;

    }
}