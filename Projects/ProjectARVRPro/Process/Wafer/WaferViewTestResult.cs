using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel

namespace ProjectARVRPro.Process.Wafer
{
    public class WaferViewTestResult : WaferTestResult
    {
        public List<PoiResultCIExyuvData> PoixyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();

        public List<SMUResultModel> SMUResultModels { get; set; } = new List<SMUResultModel>();
    }

    public class WaferTestResult : ViewModelBase
    {

    }
}
