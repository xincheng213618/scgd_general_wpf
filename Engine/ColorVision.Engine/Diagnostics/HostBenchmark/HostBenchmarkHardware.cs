using ColorVision.Common.NativeMethods;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

namespace ColorVision.Engine.Diagnostics.HostBenchmark;

internal sealed record HostBenchmarkHardware(string? CpuModel, string[] GpuModels, long? TotalMemoryMiB)
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(HostBenchmarkHardware));

    internal static HostBenchmarkHardware Read(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] processors = ReadNames("Win32_Processor");
        cancellationToken.ThrowIfCancellationRequested();
        string[] graphicsAdapters = ReadNames("Win32_VideoController");
        cancellationToken.ThrowIfCancellationRequested();
        long memoryMiB = PerformanceInfo.GetTotalMemoryInMiB();
        return new HostBenchmarkHardware(processors.Length > 0 ? string.Join(" / ", processors) : null,
            graphicsAdapters, memoryMiB > 0 ? memoryMiB : null);
    }

    private static string[] ReadNames(string hardwareClass)
    {
        try
        {
            var options = new EnumerationOptions { Timeout = TimeSpan.FromSeconds(3) };
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"root\cimv2"), new ObjectQuery($"SELECT Name FROM {hardwareClass}"), options);
            using ManagementObjectCollection rows = searcher.Get();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ManagementBaseObject row in rows)
            {
                using (row)
                {
                    if (row["Name"] is string name && !string.IsNullOrWhiteSpace(name))
                        names.Add(name.Trim());
                }
            }

            return names.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (Exception error) when (error is ManagementException or COMException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Log.Debug($"Could not read {hardwareClass} for the host benchmark.", error);
            return [];
        }
    }
}
