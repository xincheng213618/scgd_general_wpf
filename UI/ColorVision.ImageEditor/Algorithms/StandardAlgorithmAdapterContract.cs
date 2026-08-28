using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>
    /// Guards compatibility adapters that are compiled against standard parameter types. Version
    /// minor and patch changes are allowed within the canonical major version, but schema
    /// shape/defaults and the complete execution contract must remain canonical.
    /// </summary>
    internal static class StandardAlgorithmAdapterContract
    {
        private static readonly Lazy<IReadOnlyDictionary<AlgorithmId, AlgorithmDescriptor>> CanonicalDescriptors = new(
            () => StandardAlgorithmCatalog.Create().Descriptors.ToDictionary(descriptor => descriptor.Id));

        public static bool IsCompatible(AlgorithmDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return CanonicalDescriptors.Value.TryGetValue(descriptor.Id, out AlgorithmDescriptor? canonical)
                && canonical.Version.Major == descriptor.Version.Major
                && AlgorithmDescriptorContract.ExecutionShapeEquals(canonical, descriptor);
        }

        public static AlgorithmHostCapabilities GetInteractiveRequiredCapabilities(
            AlgorithmDescriptor descriptor,
            AlgorithmHostCapabilities declaredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return AlgorithmInvocationCapabilities.Derive(
                declaredCapabilities | AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                inputCount: 1,
                hasRoi: false);
        }

        public static bool TryGetInteractiveRequiredCapabilities(
            AlgorithmDescriptor descriptor,
            int inputCount,
            bool hasRoi,
            AlgorithmHostCapabilities declaredCapabilities,
            out AlgorithmHostCapabilities requiredCapabilities)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return AlgorithmInvocationCapabilities.TryPlan(
                descriptor,
                declaredCapabilities | AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                inputCount,
                hasRoi,
                out requiredCapabilities);
        }

        internal static bool IsCanonicalProviderContract(
            AlgorithmDescriptor descriptor,
            IReadOnlySet<AlgorithmId> implementedIds,
            out string? reason)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(implementedIds);
            if (!implementedIds.Contains(descriptor.Id))
            {
                reason = "algorithm_not_implemented";
                return false;
            }
            if (!IsCompatible(descriptor))
            {
                reason = "descriptor_contract_incompatible";
                return false;
            }
            reason = null;
            return true;
        }

        internal static bool IsCanonicalProviderContract(
            AlgorithmDescriptor descriptor,
            AlgorithmId implementedId,
            out string? reason)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (descriptor.Id != implementedId)
            {
                reason = "algorithm_not_implemented";
                return false;
            }
            if (!IsCompatible(descriptor))
            {
                reason = "descriptor_contract_incompatible";
                return false;
            }
            reason = null;
            return true;
        }

        public static AlgorithmDescriptor ResolveCompatible<TParameters>(
            IAlgorithmCatalog catalog,
            AlgorithmId id,
            string displayName)
            where TParameters : IAlgorithmParameters
        {
            ArgumentNullException.ThrowIfNull(catalog);
            if (!catalog.TryResolve(id, out AlgorithmDescriptor? descriptor) || descriptor == null)
                throw new InvalidOperationException($"The {displayName} descriptor is not registered.");
            if (descriptor.ParameterType != typeof(TParameters) || !IsCompatible(descriptor))
            {
                throw new InvalidOperationException(
                    $"The {displayName} descriptor parameter contract is incompatible with the built-in {typeof(TParameters).Name} adapter.");
            }
            return descriptor;
        }
    }
}
