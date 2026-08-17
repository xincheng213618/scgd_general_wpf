using ProjectARVRPro.Process.MTF.MTF07;

namespace ProjectARVRPro.Process.MTF.MTFV
{
    public sealed class MTFVProcess : MTF07DynamicProcess<MTFVProcessConfig, MTFVRecipeConfig>
    {
        protected override string Axis => "V";
    }
}
