using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Windows;

namespace ColorVision.Engine.Services.Flow
{
    public class FlowConfig:ViewModelBase,IConfig
    {
        public static FlowConfig Instance => ConfigService.Instance.GetRequiredService<FlowConfig>();

        public RelayCommand EditCommand { get; set; }
        public FlowConfig()
        {
            EditCommand = new RelayCommand(a => new EditFlowConfig() { Owner =Application.Current.GetActiveWindow(),WindowStartupLocation =WindowStartupLocation.CenterOwner }.ShowDialog());
        }


        public bool ShowWarning { get=> _ShowWarning; set { _ShowWarning = value; NotifyPropertyChanged(); } }
        private bool _ShowWarning = true;

        public long Capacity { get; set; } = 10L * 1024 * 1024 * 1024; //10GB
        [JsonIgnore]
        public string CapacityText => Common.Utilities.MemorySize.MemorySizeText(Capacity);

        [JsonIgnore]
        public string CapacityInput
        {
            get => Common.Utilities.MemorySize.MemorySizeText(Capacity);
            set
            {
                if (Common.Utilities.MemorySize.TryParseMemorySize(value, out long parsedValue))
                {
                    Capacity = parsedValue;
                }
            }
        }
    }
}
