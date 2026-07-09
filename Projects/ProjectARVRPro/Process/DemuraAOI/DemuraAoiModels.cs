using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.DemuraAOI
{
    public enum DemuraAoiOutcome
    {
        Pass,
        SpecificationNg,
        DataError
    }

    public sealed class W255UniformityResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int Radius { get; set; }
        public List<double> PointMeans { get; set; } = new List<double>();
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public double Uniformity { get; set; }
    }

    public sealed class DemuraAoiGradingData
    {
        public int MasterId { get; set; }
        public string ResultFile { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public double MaxDefectDensity { get; set; }
        public double DarkTotalDefects { get; set; }
        public double BrightTotalDefects { get; set; }
        public string TimeStamp { get; set; } = string.Empty;
    }

    public sealed class DemuraAoiBlackData
    {
        public int MasterId { get; set; }
        public string ResultFile { get; set; } = string.Empty;
        public double BrightCount { get; set; }
        public string GradeLevel { get; set; } = string.Empty;
        public string TimeStamp { get; set; } = string.Empty;
    }

    public sealed class DemuraAoiParseResult
    {
        public int BatchId { get; set; }
        public W255UniformityResult? W255 { get; set; }
        public DemuraAoiGradingData? Grading { get; set; }
        public DemuraAoiBlackData? Black { get; set; }
        public List<string> DataErrors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public bool IsDataValid => DataErrors.Count == 0;
    }

    public sealed class DemuraAoiEvaluationResult
    {
        public DemuraAoiOutcome Outcome { get; set; }
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new ObservableCollection<ObjectiveTestItem>();
        public List<string> SpecificationFailures { get; set; } = new List<string>();
        public string Message { get; set; } = string.Empty;
        public bool IsPass => Outcome == DemuraAoiOutcome.Pass;
    }
}
