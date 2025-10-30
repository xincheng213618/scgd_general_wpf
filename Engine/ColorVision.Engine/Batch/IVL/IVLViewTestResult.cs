using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Collections.Generic;

namespace ColorVision.Engine.Batch.IVL
{
    public class IVLViewTestResult : IVLTestResult
    {
        public List<PoiResultCIExyuvData> PoixyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();

        public List<SMUResultModel> SMUResultModels { get; set; } = new List<SMUResultModel>();
    }

    public class IVLTestResult : ViewModelBase
    {

    }
}
