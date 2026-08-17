using ProjectARVRPro.Process.MTF.MTF07;

namespace ProjectARVRPro.Process.MTF.MTFH
{
    public sealed class MTFHProcess : MTF07DynamicProcess<MTFHProcessConfig, MTFHRecipeConfig>
    {
        protected override string Axis => "H";
    }
}
