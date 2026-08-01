using System;

namespace ColorVision.Engine.Templates.Flow
{
    public sealed class FlowTemplateConcurrencyException :
        InvalidOperationException
    {
        public FlowTemplateConcurrencyException(
            string flowName,
            string expectedHash,
            string actualHash)
            : base(
                $"流程“{flowName}”已被其他窗口或客户端修改。"
                + "请先刷新并确认差异后再保存，当前修改未覆盖数据库内容。")
        {
            FlowName = flowName;
            ExpectedHash = expectedHash;
            ActualHash = actualHash;
        }

        public string FlowName { get; }

        public string ExpectedHash { get; }

        public string ActualHash { get; }
    }
}
