using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel

namespace ProjectLUX.Process.VID
{

    public class VIDTestResult : ViewModelBase
    {
        public ObjectiveTestItem VID { get; set; } = new ObjectiveTestItem() { Name = "VID", Unit = "cd/m^2" };
    }
    }
            