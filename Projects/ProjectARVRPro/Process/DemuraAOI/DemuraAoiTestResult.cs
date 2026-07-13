using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.DemuraAOI
{
    public class DemuraAoiViewTestResult : DemuraAoiTestResult
    {
    }

    public class DemuraAoiTestResult : ViewModelBase
    {
        public string Outcome { get; set; } = DemuraAoiOutcome.DataError.ToString();
        public string Message { get; set; } = string.Empty;
        public W255UniformityResult? W255 { get; set; }
        public DemuraAoiGradingData? Grading { get; set; }
        public DemuraAoiBlackData? Black { get; set; }
        public DemuraAoiSensorData? Sensor { get; set; }
        public DemuraAoiSpectrometerData? Spectrometer { get; set; }
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new ObservableCollection<ObjectiveTestItem>();
        public List<string> DataErrors { get; set; } = new List<string>();
        public List<string> SpecificationFailures { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
