using ProjectARVRPro.Process.MTF.MTF07;
using ProjectARVRPro.Process.KeyedResults;

namespace ProjectARVRPro.Process.MTF.MTF07.MTFV
{
    /// <summary>
    /// MTF07测试点位方案的竖条纹流程：中心0F和四角0.7F共五个点位。
    /// </summary>
    public sealed class MTFV07Process : MTF07DynamicProcess<MTFV07ProcessConfig, MTFV07RecipeConfig, MTFV07ViewTestResult, MTFV07TestResult>
    {
        protected override void WriteObjectiveResult(ObjectiveTestResult destination, string key, MTFV07TestResult result)
            => KeyedTestResultWriter.Write(destination, key, result);
    }
}
