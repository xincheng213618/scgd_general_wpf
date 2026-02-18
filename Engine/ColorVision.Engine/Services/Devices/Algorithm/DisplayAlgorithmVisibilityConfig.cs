using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class DisplayAlgorithmVisibilityConfig : ViewModelBase, IConfig
    {
        public static DisplayAlgorithmVisibilityConfig Instance => ConfigService.Instance.GetRequiredService<DisplayAlgorithmVisibilityConfig>();

        public Dictionary<string, bool> AlgorithmVisibility { get; set; } = new Dictionary<string, bool>();

        public bool GetAlgorithmVisibility(string name)
        {
            if (string.IsNullOrEmpty(name))
                return true;

            if (AlgorithmVisibility.TryGetValue(name, out bool isVisible))
                return isVisible;

            return true;
        }

        public void SetAlgorithmVisibility(string name, bool isVisible)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (AlgorithmVisibility.ContainsKey(name))
                AlgorithmVisibility[name] = isVisible;
            else
                AlgorithmVisibility.Add(name, isVisible);

            OnPropertyChanged(nameof(AlgorithmVisibility));
        }
    }
}
