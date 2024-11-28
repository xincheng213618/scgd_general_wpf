using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Flow;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
{
    public class FlowConfig : ViewModelBase, IConfig
    {
        public static FlowConfig Instance => ConfigService.Instance.GetRequiredService<FlowConfig>();

        public RelayCommand EditCommand { get; set; }
        public FlowConfig()
        {
            EditCommand = new RelayCommand(a => new EditFlowConfig() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }
        public bool IsAutoEditSave { get => _IsAutoEditSave; set { _IsAutoEditSave = value; NotifyPropertyChanged(); } }
        private bool _IsAutoEditSave = true;
        public bool IsAutoSize { get => _IsAutoSize; set { _IsAutoSize = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSize = true;

        public bool ShowWarning { get => _ShowWarning; set { _ShowWarning = value; NotifyPropertyChanged(); } }
        private bool _ShowWarning = true;

        public long Capacity { get; set; } = 10L * 1024 * 1024 * 1024; //10GB
        [JsonIgnore]
        public string CapacityText => MemorySize.MemorySizeText(Capacity);

        [JsonIgnore]
        public string CapacityInput
        {
            get => MemorySize.MemorySizeText(Capacity);
            set
            {
                if (MemorySize.TryParseMemorySize(value, out long parsedValue))
                {
                    Capacity = parsedValue;
                }
            }
        }
    }
}
