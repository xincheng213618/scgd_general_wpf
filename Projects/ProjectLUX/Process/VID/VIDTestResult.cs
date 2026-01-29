using ColorVision.Common.MVVM;

namespace ProjectLUX.Process.VID
{

    public class VIDTestResult : ViewModelBase
    {
        public ObjectiveTestItem VID { get; set; } = new ObjectiveTestItem() { Name = "VID", Unit = "cd/m^2" };
    }
    }
            