using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class DisplayAlgorithmVisibilityConfig : ViewModelBase, IConfig
    {
        public static DisplayAlgorithmVisibilityConfig Instance => ConfigService.Instance.GetRequiredService<DisplayAlgorithmVisibilityConfig>();

        public Dictionary<string, bool> AlgorithmVisibility { get; set; } = new Dictionary<string, bool>();

        public Dictionary<string, int> OrderOverrides { get; set; } = new Dictionary<string, int>();

        public Dictionary<string, string> NameOverrides { get; set; } = new Dictionary<string, string>();

        public event EventHandler Changed;

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

            AlgorithmVisibility[name] = isVisible;
            OnPropertyChanged(nameof(AlgorithmVisibility));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public int GetOrderOverride(string name, int defaultOrder)
        {
            if (!string.IsNullOrEmpty(name) && OrderOverrides.TryGetValue(name, out int order))
                return order;
            return defaultOrder;
        }

        public void SetOrderOverride(string name, int order)
        {
            if (string.IsNullOrEmpty(name))
                return;

            OrderOverrides[name] = order;
            OnPropertyChanged(nameof(OrderOverrides));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public string GetNameOverride(string name)
        {
            if (!string.IsNullOrEmpty(name) && NameOverrides.TryGetValue(name, out string displayName) && !string.IsNullOrEmpty(displayName))
                return displayName;
            return name;
        }

        public void SetNameOverride(string name, string displayName)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (string.IsNullOrEmpty(displayName) || displayName == name)
            {
                NameOverrides.Remove(name);
            }
            else
            {
                NameOverrides[name] = displayName;
            }
            OnPropertyChanged(nameof(NameOverrides));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
