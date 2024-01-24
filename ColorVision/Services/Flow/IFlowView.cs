namespace ColorVision.Services.Flow
{
    public interface IFlowView
    {
        public View View { get; set; }
        public FlowEngineLib.FlowEngineControl FlowEngineControl { get; set; }
    }
}
