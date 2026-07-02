using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.Demura
{
    public class DemuraViewTestResult : DemuraTestResult
    {
    }

    public class DemuraTestResult : ViewModelBase
    {
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new ObservableCollection<ObjectiveTestItem>();

        public DemuraCsvFileResult W128 { get; set; } = new DemuraCsvFileResult { Gray = "128" };

        public DemuraCsvFileResult W255 { get; set; } = new DemuraCsvFileResult { Gray = "255" };

        public string PreviewImageFile { get; set; } = string.Empty;

        public string ToolDirectory { get; set; } = string.Empty;

        public string WorkDirectory { get; set; } = string.Empty;

        public string CsvDirectory { get; set; } = string.Empty;

        public string ToolExecutable { get; set; } = string.Empty;

        public string DemuraConfigFile { get; set; } = string.Empty;

        public string StaticBinFile { get; set; } = string.Empty;

        public string DynamicBinFile { get; set; } = string.Empty;

        public string MergedBinFile { get; set; } = string.Empty;

        public bool ToolPrepared { get; set; }

        public bool ToolLaunched { get; set; }

        public bool BinGenerated { get; set; }

        public bool MergedBinExists { get; set; }

        public bool BurnEnabled { get; set; }

        public bool BurnSucceeded { get; set; }

        public string BurnSourceFile { get; set; } = string.Empty;

        public string BurnTargetFileName { get; set; } = string.Empty;

        public string BurnSensorCode { get; set; } = string.Empty;

        public string BurnSensorName { get; set; } = string.Empty;

        public string BurnAddress { get; set; } = string.Empty;

        public int BurnPort { get; set; }

        public string BurnCommand { get; set; } = string.Empty;

        public string BurnCommandHex { get; set; } = string.Empty;

        public string BurnResponseText { get; set; } = string.Empty;

        public string BurnResponseHex { get; set; } = string.Empty;

        public string BurnMessage { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    public class DemuraCsvFileResult : ViewModelBase
    {
        public string Gray { get; set; } = string.Empty;

        public string SourceFile { get; set; } = string.Empty;

        public string PreparedFile { get; set; } = string.Empty;

        public double ExposureTime { get; set; } = 1;

        public string SourceField { get; set; } = string.Empty;

        public int MasterId { get; set; }

        public bool SourceExists { get; set; }

        public long FileSize { get; set; }

        public int LineCount { get; set; }

        public int ValueCount { get; set; }

        public double Min { get; set; }

        public double Max { get; set; }

        public double Average { get; set; }

        public string ParseMessage { get; set; } = string.Empty;
    }
}
