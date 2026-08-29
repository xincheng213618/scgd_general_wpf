using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>
    /// Default-product availability decorator for implementations that remain in the source and
    /// catalog but have not completed release validation. It reuses the existing provider
    /// availability contract so menu projection and direct Runner execution fail closed together.
    /// </summary>
    internal sealed class ExperimentalAlgorithmProviderGate(
        IImageAlgorithmProvider inner,
        string reasonCode,
        string reason) : IImageAlgorithmProvider, IAlgorithmDescriptorSupport, IAlgorithmProviderAvailability
    {
        private readonly IImageAlgorithmProvider _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        private readonly string _reasonCode = Require(reasonCode, nameof(reasonCode));
        private readonly string _reason = Require(reason, nameof(reason));

        public AlgorithmProviderMetadata Metadata => _inner.Metadata;

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            if (_inner is IAlgorithmDescriptorSupport descriptorSupport)
                return descriptorSupport.CanExecuteDescriptor(descriptor, out reason);
            return _inner.CanExecuteDescriptor(descriptor, out reason);
        }

        public bool IsAvailable(AlgorithmDescriptor descriptor, out string? reason)
        {
            if (!CanExecuteDescriptor(descriptor, out reason)) return false;
            reason = ExperimentalReason;
            return false;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            if (!CanExecuteDescriptor(descriptor, out reason)) return false;
            reason = ExperimentalReason;
            return false;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            return ValueTask.FromResult(new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures =
                [
                    new AlgorithmFailure(
                        "algorithm_experimental",
                        _reason,
                        Details: new Dictionary<string, string> { ["reasonCode"] = _reasonCode }),
                ],
            });
        }

        private string ExperimentalReason => $"algorithm_experimental ({_reasonCode}): {_reason}";

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
            return value.Trim();
        }
    }
}
