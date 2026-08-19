using ProjectARVRPro.Process.MTF.MTF07;
using ProjectARVRPro.Process.KeyedResults;

namespace ProjectARVRPro.Process.MTF.MTF07.MTFH
{
    /// <summary>
    /// MTF07测试点位方案的横条纹流程：中心0F和四角0.7F共五个点位。
    /// </summary>
    public sealed class MTFH07Process : MTF07DynamicProcess<MTFH07ProcessConfig, MTFH07RecipeConfig, MTFH07ViewTestResult, MTFH07TestResult>
    {
        protected override void WriteObjectiveResult(ObjectiveTestResult destination, string key, MTFH07TestResult result)
            => KeyedTestResultWriter.Write(destination, key, result);
    }
}
