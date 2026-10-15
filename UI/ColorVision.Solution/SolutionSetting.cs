using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.Solution
{

    [DisplayName("SolutionConfiguration")]
    public class SolutionSetting: ViewModelBase,IConfig
    {
        public static SolutionSetting Instance => ConfigService.Instance.GetRequiredService<SolutionSetting>();

        // Keep the persisted key and value compatible with existing user configurations.
        // New workspaces can be named directly in the creation dialog.
        public string DefaultCreatName { get => _DefaultCreatName; set { _DefaultCreatName = value; OnPropertyChanged(); } }
        private string _DefaultCreatName = ColorVision.Solution.Properties.Resources.NewSolution;

    }
}
