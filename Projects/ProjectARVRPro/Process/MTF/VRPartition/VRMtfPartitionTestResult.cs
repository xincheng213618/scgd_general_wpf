using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.MTF.VRPartition;

public sealed class VRMtfPartitionViewTestResult : VRMtfPartitionTestResult
{
    public MTFDetailViewReslut? MTFDetailViewReslut { get; set; }
}

public class VRMtfPartitionTestResult : ViewModelBase
{
    public int PointCount { get; set; }
    public Dictionary<string, int> RegionPointCounts { get; set; } = new();
    public List<int> PointRegions { get; set; } = new();

    // Original grid points followed by the twelve regional metrics. The common
    // objective collector/exporter uses Items for both history and live output.
    public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new();
}
