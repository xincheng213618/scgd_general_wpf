
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ProjectARVRPro.Process.W255;

namespace ProjectARVRPro.Process.Black
{

    public class BlackViewTestResult : BlackTestResult
    {
        public List<PoiResultCIExyuvData> ViewPoixyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();
    }

    public class BlackTestResult : ViewModelBase
    {
        public List<PoixyuvData> PoixyuvDatas { get; set; } = new List<PoixyuvData>();

        /// <summary>
        /// FOFO对比度 测试项
        /// </summary>
        public ObjectiveTestItem FOFOContrast { get; set; } = new ObjectiveTestItem() { Name = "FOFOContrast", Unit = "%" };


    }
}