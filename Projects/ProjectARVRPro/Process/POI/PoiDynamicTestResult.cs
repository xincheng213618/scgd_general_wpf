using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;

namespace ProjectARVRPro.Process.POI
{
    public class PoiDynamicTestResult : ViewModelBase
    {
        public List<PoiResultCIExyuvData> ViewPoixyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();
        public List<PoixyuvData> PoixyuvDatas { get; set; } = new List<PoixyuvData>();
    }
}
