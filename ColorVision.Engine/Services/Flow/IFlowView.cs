using ColorVision.UI.Views;

namespace ColorVision.Services.Flow
{
    public interface IFlowView:IView
    {
        public FlowEngineLib.FlowEngineControl FlowEngineControl { get; set; }
    }
}
