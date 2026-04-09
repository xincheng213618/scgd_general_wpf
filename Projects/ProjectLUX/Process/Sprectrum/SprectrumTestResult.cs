using ColorVision.Common.MVVM;

namespace ProjectLUX.Process.Sprectrum
{

    public class SprectrumTestResult : ViewModelBase
    {
        public ObjectiveTestItem LuminousFlux { get; set; } = new ObjectiveTestItem() { Name = "LuminousFlux", Unit = "lm" };
    }
}
            