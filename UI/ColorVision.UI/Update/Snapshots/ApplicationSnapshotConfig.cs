using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.Update
{
    public sealed class ApplicationSnapshotConfig : ViewModelBase, IConfig
    {
        public static ApplicationSnapshotConfig Instance => ConfigService.Instance.GetRequiredService<ApplicationSnapshotConfig>();

        [ConfigSetting(Order = 510, Section = ConfigSettingConstants.SectionBasic, Description = "CreateSnapshotBeforeUpdateDescription")]
        [DisplayName("CreateSnapshotBeforeUpdate")]
        public bool CreateSnapshotBeforeUpdate
        {
            get => _createSnapshotBeforeUpdate;
            set
            {
                if (_createSnapshotBeforeUpdate == value)
                    return;

                _createSnapshotBeforeUpdate = value;
                OnPropertyChanged();
            }
        }
        private bool _createSnapshotBeforeUpdate;
    }
}
