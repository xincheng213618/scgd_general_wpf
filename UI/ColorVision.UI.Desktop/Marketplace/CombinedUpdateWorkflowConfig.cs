using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.UI.Desktop.Marketplace
{
    public enum CombinedUpdateStage
    {
        Idle = 0,
        UpdatingApplication = 1,
        UpdatingPlugins = 2,
    }

    public class CombinedUpdateWorkflowConfig : ViewModelBase, IConfig
    {
        public static CombinedUpdateWorkflowConfig Instance => ConfigService.Instance.GetRequiredService<CombinedUpdateWorkflowConfig>();

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
        private bool _isActive;

        public bool UpdatePluginsAfterApplication
        {
            get => _updatePluginsAfterApplication;
            set
            {
                _updatePluginsAfterApplication = value;
                OnPropertyChanged();
            }
        }
        private bool _updatePluginsAfterApplication = true;

        public CombinedUpdateStage Stage
        {
            get => _stage;
            set
            {
                _stage = value;
                OnPropertyChanged();
            }
        }
        private CombinedUpdateStage _stage = CombinedUpdateStage.Idle;

        public void Activate(CombinedUpdateStage stage, bool updatePluginsAfterApplication = true)
        {
            IsActive = true;
            UpdatePluginsAfterApplication = updatePluginsAfterApplication;
            Stage = stage;
        }

        public void Clear()
        {
            IsActive = false;
            UpdatePluginsAfterApplication = true;
            Stage = CombinedUpdateStage.Idle;
        }
    }
}