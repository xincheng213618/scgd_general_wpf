using ColorVision.Algorithms;
using ColorVision.Core;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    public sealed class NativeCompatibilityAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport, IAlgorithmProviderAvailability
    {
        private const string LibraryName = "opencv_helper.dll";
        private const string RemoveMoireExport = "M_RemoveMoire";
        private static readonly IReadOnlySet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();
        private readonly Func<NativeAlgorithmAvailability> _removeMoireAvailability;

        public NativeCompatibilityAlgorithmProvider()
            : this(ProbeRemoveMoireAvailability)
        {
        }

        internal NativeCompatibilityAlgorithmProvider(Func<NativeAlgorithmAvailability> removeMoireAvailability)
        {
            _removeMoireAvailability = removeMoireAvailability ?? throw new ArgumentNullException(nameof(removeMoireAvailability));
        }

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.native.compatibility",
            "ColorVision Native Compatibility",
            AlgorithmProviderKind.Native,
            AlgorithmExecutionPlane.Local,
            90,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic,
            Formats);

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
            => StandardAlgorithmAdapterContract.IsCanonicalProviderContract(
                descriptor,
                StandardAlgorithmIds.RemoveMoire,
                out reason);

        public bool IsAvailable(AlgorithmDescriptor descriptor, out string? reason)
        {
            if (!CanExecuteDescriptor(descriptor, out reason)) return false;
            NativeAlgorithmAvailability availability = _removeMoireAvailability();
            reason = availability.IsAvailable ? null : $"native_dependency_unavailable: {availability.Reason}";
            return availability.IsAvailable;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            if (descriptor.Id != StandardAlgorithmIds.RemoveMoire || inputs.Count != 1)
            {
                reason = "algorithm_not_implemented";
                return false;
            }

            NativeAlgorithmAvailability availability = _removeMoireAvailability();
            if (!availability.IsAvailable)
            {
                reason = $"native_dependency_unavailable: {availability.Reason}";
                return false;
            }

            reason = null;
            return true;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmInput input = context.Inputs[0];
            using MemoryHandle sourcePin = input.Image.Data.Pin();
            HImage source = CreateBorrowedHImage(input.Image, sourcePin);
            int code;
            HImage output;
            try
            {
                code = OpenCVMediaHelper.M_RemoveMoire(source, out output);
            }
            catch (Exception exception) when (exception is DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or FileLoadException)
            {
                return ValueTask.FromResult(NativeUnavailable(context, exception.Message));
            }
            if (code != 0)
            {
                output.Dispose();
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Failed,
                    Failures = new[] { new AlgorithmFailure("native_error", $"Native RemoveMoire returned {code}.") },
                });
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                AlgorithmImageBuffer resultImage = ImageAlgorithmInputFactory.Copy(
                    output,
                    input.Image.Format,
                    input.Image.DpiX,
                    input.Image.DpiY);
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts = new AlgorithmArtifact[] { new AlgorithmImageArtifact("image", "primary", resultImage) },
                });
            }
            finally
            {
                output.Dispose();
            }
        }

        private static NativeAlgorithmAvailability ProbeRemoveMoireAvailability()
        {
            return NativeModule.ProbeRemoveMoire();
        }

        private static class NativeModule
        {
            private static readonly object SyncRoot = new();
            private static IntPtr _module;

            public static NativeAlgorithmAvailability ProbeRemoveMoire()
            {
                if (!OperatingSystem.IsWindows())
                    return NativeAlgorithmAvailability.Unavailable($"{LibraryName} is supported only on Windows.");

                lock (SyncRoot)
                {
                    if (_module != IntPtr.Zero) return NativeAlgorithmAvailability.Available;

                    List<string> attempts = new();
                    if (TryLoadByApplicationPolicy(out IntPtr module))
                    {
                        NativeAlgorithmAvailability availability = AcceptModule(module);
                        if (availability.IsAvailable) return availability;
                        attempts.Add(availability.Reason);
                    }
                    else
                    {
                        attempts.Add($"{LibraryName} was not found by the application native-library search policy.");
                    }

                    string runtimePath = Path.Combine(
                        AppContext.BaseDirectory,
                        "runtimes",
                        "win-x64",
                        "native",
                        LibraryName);
                    if (!File.Exists(runtimePath))
                    {
                        attempts.Add($"The packaged runtime was not found at {runtimePath}.");
                    }
                    else if (TryLoadRuntime(runtimePath, out module, out string? loadError))
                    {
                        NativeAlgorithmAvailability availability = AcceptModule(module);
                        if (availability.IsAvailable) return availability;
                        attempts.Add(availability.Reason);
                    }
                    else
                    {
                        attempts.Add(loadError ?? $"The packaged runtime at {runtimePath} could not be loaded.");
                    }

                    return NativeAlgorithmAvailability.Unavailable(string.Join(" ", attempts));
                }
            }

            private static bool TryLoadByApplicationPolicy(out IntPtr module)
            {
                try
                {
                    return NativeLibrary.TryLoad(
                        LibraryName,
                        typeof(OpenCVMediaHelper).Assembly,
                        DllImportSearchPath.SafeDirectories,
                        out module);
                }
                catch (Exception exception) when (IsNativeLoadException(exception))
                {
                    module = IntPtr.Zero;
                    return false;
                }
            }

            private static bool TryLoadRuntime(string runtimePath, out IntPtr module, out string? error)
            {
                try
                {
                    bool loaded = NativeLibrary.TryLoad(runtimePath, out module);
                    error = loaded ? null : $"The packaged runtime at {runtimePath} could not be loaded.";
                    return loaded;
                }
                catch (Exception exception) when (IsNativeLoadException(exception))
                {
                    module = IntPtr.Zero;
                    error = exception.Message;
                    return false;
                }
            }

            private static NativeAlgorithmAvailability AcceptModule(IntPtr module)
            {
                if (!NativeLibrary.TryGetExport(module, RemoveMoireExport, out _))
                {
                    NativeLibrary.Free(module);
                    return NativeAlgorithmAvailability.Unavailable($"{LibraryName} does not export {RemoveMoireExport}.");
                }

                // Keep the validated module loaded for the process lifetime. The existing
                // DllImport call resolves it by basename and must not race an availability
                // probe that unloads the module immediately before execution.
                _module = module;
                return NativeAlgorithmAvailability.Available;
            }

            private static bool IsNativeLoadException(Exception exception) => exception is DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or FileLoadException;
        }

        private static AlgorithmResult NativeUnavailable(AlgorithmExecutionContext context, string reason) => new()
        {
            InvocationId = context.Invocation.InvocationId,
            AlgorithmId = context.Descriptor.Id,
            AlgorithmVersion = context.Descriptor.Version,
            Status = AlgorithmResultStatus.Failed,
            Failures = new[]
            {
                new AlgorithmFailure(
                    "native_provider_unavailable",
                    $"{LibraryName} with export {RemoveMoireExport} is unavailable: {reason}"),
            },
        };

        private static unsafe HImage CreateBorrowedHImage(AlgorithmImageBuffer image, MemoryHandle pin)
        {
            return new HImage
            {
                rows = image.Height,
                cols = image.Width,
                channels = image.Format.Channels(),
                depth = image.Format.BitsPerChannel(),
                stride = image.Stride,
                isDispose = true,
                pData = (IntPtr)pin.Pointer,
            };
        }

    }

    internal readonly record struct NativeAlgorithmAvailability(bool IsAvailable, string Reason)
    {
        public static NativeAlgorithmAvailability Available { get; } = new(true, string.Empty);

        public static NativeAlgorithmAvailability Unavailable(string reason) => new(
            false,
            string.IsNullOrWhiteSpace(reason) ? "The native dependency is unavailable." : reason);
    }
}
