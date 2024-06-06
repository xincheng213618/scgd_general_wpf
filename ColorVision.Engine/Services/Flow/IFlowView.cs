using ColorVision.UI.Views;

namespace ColorVision.Engine.Services.Flow
{
    public interface IFlowView:IView
    {
        public FlowEngineLib.FlowEngineControl FlowEngineControl { get; set; }
    }
}
