#pragma warning disable CA1401,CA1707,CA2101
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ColorVision.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CalibrationExecutionOptionsV1
    {
        public uint StructSize;
        public int InterleavedBgr;
        public int RgbType;
        public uint RoiX;
        public uint RoiY;
        public uint RoiWidth;
        public uint RoiHeight;
        public uint ObLeft;
        public uint ObRight;
        public uint ObTop;
        public uint ObBottom;
        public float ExposureX;
        public float ExposureY;
        public float ExposureZ;

        public static CalibrationExecutionOptionsV1 Create(float[] exposure)
        {
            ArgumentNullException.ThrowIfNull(exposure);
            float exposureX = exposure.Length > 0 ? exposure[0] : 0;
            float exposureY = exposure.Length > 1 ? exposure[1] : exposureX;
            float exposureZ = exposure.Length > 2 ? exposure[2] : exposureY;

            return new CalibrationExecutionOptionsV1
            {
                StructSize = checked((uint)Marshal.SizeOf<CalibrationExecutionOptionsV1>()),
                InterleavedBgr = 1,
                ExposureX = exposureX,
                ExposureY = exposureY,
                ExposureZ = exposureZ,
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PoiRequestV1
    {
        public int Type;
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PoiResultV1
    {
        public float X;
        public float Y;
        public float Z;
        public float ChromaX;
        public float ChromaY;
        public float u;
        public float v;
        public float Cct;
        public float Wave;
    }

    [Flags]
    public enum PoiOptionsFlagsV2 : uint
    {
        None = 0,
        PercentThreshold = 1,
        ApplyMnp = 2,
        PreserveNonPositiveValues = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PoiOptionsV2
    {
        public uint StructSize;
        public PoiOptionsFlagsV2 Flags;
        public int FilterMode;
        public int XyzChannel;
        public float Threshold;
        public float MaxPercent;
        public float ScaleX;
        public float ScaleY;
        public float ScaleZ;
        private uint Reserved0;
        private uint Reserved1;
        private uint Reserved2;

        public static PoiOptionsV2 Create()
        {
            return new PoiOptionsV2
            {
                StructSize = checked((uint)Marshal.SizeOf<PoiOptionsV2>()),
                ScaleX = 1,
                ScaleY = 1,
                ScaleZ = 1,
            };
        }
    }

    /// <summary>
    /// Process-wide native calibration asset cache statistics.
    /// </summary>
    public sealed record CalibrationSharedCacheStatistics(
        uint EntryCount,
        ulong Generation,
        ulong EstimatedMemoryBytes,
        ulong BudgetBytes,
        ulong HitCount,
        ulong MissCount);

    [Flags]
    public enum CalibrationSharedCacheEntryStates : uint
    {
        None = 0,
        Loading = 1,
        Ready = 2,
    }

    /// <summary>
    /// One immutable calibration file asset retained by opencv_helper.
    /// </summary>
    public sealed record CalibrationSharedCacheEntry(
        int CalibrationType,
        CalibrationSharedCacheEntryStates Flags,
        string FilePath,
        ulong FileBytes,
        ulong EstimatedMemoryBytes,
        ulong HitCount,
        ulong LastAccessSequence,
        uint ActiveOwnerCount);

    public sealed record CalibrationSharedCacheSnapshot(
        CalibrationSharedCacheStatistics Statistics,
        IReadOnlyList<CalibrationSharedCacheEntry> Entries);

    /// <summary>
    /// Result of removing the process-wide strong references. Assets that are
    /// still referenced by a live calibration context remain valid and are
    /// reported separately.
    /// </summary>
    public sealed record CalibrationSharedCacheReleaseResult(
        uint ReleasedEntryCount,
        ulong ReleasedEstimatedMemoryBytes,
        uint ActiveEntryCount,
        uint ActiveOwnerCount,
        ulong ActiveEstimatedMemoryBytes,
        ulong Generation);

    /// <summary>
    /// Native calibration and colorimetric POI interop backed by opencv_helper.
    /// </summary>
    public static class OpenCVCalibration
    {
        private const string LibPath = "opencv_helper.dll";
        private const int CacheEnumerationAttempts = 3;
        private const uint MaxCacheEntryCount = 65_536;
        private const uint MaxCachePathCharacterCount = 1_048_576;
        public const int CalibrationOk = 1;
        public const int PoiOk = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct CalibrationCacheStatsV1
        {
            public uint StructSize;
            public uint EntryCount;
            public ulong Generation;
            public ulong EstimatedMemoryBytes;
            public ulong BudgetBytes;
            public ulong HitCount;
            public ulong MissCount;

            public static CalibrationCacheStatsV1 Create()
                => new() { StructSize = checked((uint)Marshal.SizeOf<CalibrationCacheStatsV1>()) };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CalibrationCacheEntryV1
        {
            public uint StructSize;
            public int CalibrationType;
            public uint Flags;
            public uint PathCharacterCount;
            public ulong Generation;
            public ulong FileBytes;
            public ulong EstimatedMemoryBytes;
            public ulong HitCount;
            public ulong LastAccessSequence;
            public uint ActiveOwnerCount;
            public uint Reserved;

            public static CalibrationCacheEntryV1 Create()
                => new() { StructSize = checked((uint)Marshal.SizeOf<CalibrationCacheEntryV1>()) };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CalibrationCacheReleaseResultV1
        {
            public uint StructSize;
            public uint ReleasedEntryCount;
            public ulong ReleasedEstimatedMemoryBytes;
            public uint ActiveEntryCount;
            public uint ActiveOwnerCount;
            public ulong ActiveEstimatedMemoryBytes;
            public ulong Generation;

            public static CalibrationCacheReleaseResultV1 Create()
                => new() { StructSize = checked((uint)Marshal.SizeOf<CalibrationCacheReleaseResultV1>()) };
        }

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_CalibrationCreate(out IntPtr context);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_CalibrationDestroy(IntPtr context);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_CalibrationClear(IntPtr context);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int M_CalibrationLoadFileW(IntPtr context, int calibrationType, string filePath);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_CalibrationExecute(
            IntPtr context,
            uint width,
            uint height,
            uint bitsPerChannel,
            uint channels,
            IntPtr rawData,
            ulong rawByteLength,
            IntPtr cieData,
            ulong cieFloatCount,
            in CalibrationExecutionOptionsV1 options);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_CalibrationExecuteToV1(
            IntPtr context,
            uint width,
            uint height,
            uint bitsPerChannel,
            uint channels,
            IntPtr sourceRawData,
            ulong sourceRawByteLength,
            IntPtr correctedRawData,
            ulong correctedRawByteLength,
            IntPtr cieData,
            ulong cieFloatCount,
            in CalibrationExecutionOptionsV1 options);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_CalibrationGetLastError(IntPtr context, [Out] byte[]? buffer, uint bufferLength);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_CalibrationGetItemCount(IntPtr context);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int M_CalibrationCacheGetStatsV1(ref CalibrationCacheStatsV1 statistics);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int M_CalibrationCacheGetEntryV1(
            uint index,
            ref CalibrationCacheEntryV1 entry,
            IntPtr path,
            uint pathCapacity);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int M_CalibrationCacheReleaseV1(ref CalibrationCacheReleaseResultV1 result);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_CalculatePoiBatchV1(
            int width,
            int height,
            int bitsPerChannel,
            int channels,
            IntPtr cieData,
            ulong cieFloatCount,
            [In] PoiRequestV1[] requests,
            uint requestCount,
            [Out] PoiResultV1[] results);

        [DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int M_CalculatePoiBatchV2(
            int width,
            int height,
            int bitsPerChannel,
            int channels,
            IntPtr cieData,
            ulong cieFloatCount,
            [In] PoiRequestV1[] requests,
            uint requestCount,
            in PoiOptionsV2 options,
            [Out] PoiResultV1[] results);

        public static string GetCalibrationError(IntPtr context)
        {
            int required = M_CalibrationGetLastError(context, null, 0);
            for (int attempt = 0; attempt < 3 && required > 1; attempt++)
            {
                byte[] buffer = new byte[required];
                int result = M_CalibrationGetLastError(context, buffer, checked((uint)buffer.Length));
                if (result <= 1) return string.Empty;
                if (result <= buffer.Length) return Encoding.UTF8.GetString(buffer, 0, result - 1);
                required = result;
            }
            return required <= 1 ? string.Empty : "Native calibration error changed while being read.";
        }

        /// <summary>
        /// Reads a coherent process-wide calibration asset cache snapshot.
        /// Concurrent cache mutations restart the bounded enumeration.
        /// </summary>
        public static CalibrationSharedCacheSnapshot GetCalibrationSharedCacheEntries()
        {
            try
            {
                for (int attempt = 0; attempt < CacheEnumerationAttempts; attempt++)
                {
                    CalibrationCacheStatsV1 before = ReadCalibrationCacheStatistics();
                    if (before.EntryCount > MaxCacheEntryCount)
                    {
                        throw new InvalidOperationException(
                            $"Native calibration cache reported too many entries: {before.EntryCount}.");
                    }

                    List<CalibrationSharedCacheEntry> entries = new(checked((int)before.EntryCount));
                    bool changed = false;
                    for (uint index = 0; index < before.EntryCount; index++)
                    {
                        if (!TryReadCalibrationCacheEntry(index, before.Generation, out CalibrationSharedCacheEntry entry))
                        {
                            changed = true;
                            break;
                        }
                        entries.Add(entry);
                    }

                    CalibrationCacheStatsV1 after = ReadCalibrationCacheStatistics();
                    if (!changed && after.Generation == before.Generation && after.EntryCount == before.EntryCount)
                    {
                        return new CalibrationSharedCacheSnapshot(ToManaged(after), entries.AsReadOnly());
                    }
                }

                throw new InvalidOperationException(
                    "Native calibration cache changed repeatedly while it was being enumerated. Please retry.");
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                throw CreateCalibrationCacheCompatibilityException(ex);
            }
        }

        /// <summary>
        /// Drops process-wide native strong references. Live calibration
        /// contexts keep their assets alive until those contexts are released.
        /// </summary>
        public static CalibrationSharedCacheReleaseResult ClearCalibrationSharedCache()
        {
            try
            {
                CalibrationCacheReleaseResultV1 nativeResult = CalibrationCacheReleaseResultV1.Create();
                int result = M_CalibrationCacheReleaseV1(ref nativeResult);
                EnsureCalibrationCacheResult(result, nameof(M_CalibrationCacheReleaseV1));
                return new CalibrationSharedCacheReleaseResult(
                    nativeResult.ReleasedEntryCount,
                    nativeResult.ReleasedEstimatedMemoryBytes,
                    nativeResult.ActiveEntryCount,
                    nativeResult.ActiveOwnerCount,
                    nativeResult.ActiveEstimatedMemoryBytes,
                    nativeResult.Generation);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                throw CreateCalibrationCacheCompatibilityException(ex);
            }
        }

        private static CalibrationCacheStatsV1 ReadCalibrationCacheStatistics()
        {
            CalibrationCacheStatsV1 statistics = CalibrationCacheStatsV1.Create();
            int result = M_CalibrationCacheGetStatsV1(ref statistics);
            EnsureCalibrationCacheResult(result, nameof(M_CalibrationCacheGetStatsV1));
            return statistics;
        }

        private static bool TryReadCalibrationCacheEntry(
            uint index,
            ulong expectedGeneration,
            out CalibrationSharedCacheEntry entry)
        {
            entry = null!;
            CalibrationCacheEntryV1 nativeEntry = CalibrationCacheEntryV1.Create();
            int result = M_CalibrationCacheGetEntryV1(index, ref nativeEntry, IntPtr.Zero, 0);
            if (result != CalibrationOk) return CacheGenerationChanged(expectedGeneration);
            if (nativeEntry.Generation != expectedGeneration) return false;

            uint pathCharacterCount = nativeEntry.PathCharacterCount;
            if (pathCharacterCount == 0 || pathCharacterCount > MaxCachePathCharacterCount)
            {
                throw new InvalidOperationException(
                    $"Native calibration cache returned an invalid path length: {pathCharacterCount}.");
            }

            int pathBytes = checked((int)pathCharacterCount * sizeof(char));
            IntPtr pathBuffer = Marshal.AllocHGlobal(pathBytes);
            try
            {
                nativeEntry = CalibrationCacheEntryV1.Create();
                result = M_CalibrationCacheGetEntryV1(index, ref nativeEntry, pathBuffer, pathCharacterCount);
                if (result != CalibrationOk) return CacheGenerationChanged(expectedGeneration);
                if (nativeEntry.Generation != expectedGeneration) return false;
                if (nativeEntry.PathCharacterCount == 0 || nativeEntry.PathCharacterCount > pathCharacterCount)
                {
                    return false;
                }

                string path = Marshal.PtrToStringUni(pathBuffer, checked((int)nativeEntry.PathCharacterCount - 1))
                    ?? string.Empty;
                entry = new CalibrationSharedCacheEntry(
                    nativeEntry.CalibrationType,
                    (CalibrationSharedCacheEntryStates)nativeEntry.Flags,
                    path,
                    nativeEntry.FileBytes,
                    nativeEntry.EstimatedMemoryBytes,
                    nativeEntry.HitCount,
                    nativeEntry.LastAccessSequence,
                    nativeEntry.ActiveOwnerCount);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(pathBuffer);
            }
        }

        private static bool CacheGenerationChanged(ulong expectedGeneration)
        {
            CalibrationCacheStatsV1 current = ReadCalibrationCacheStatistics();
            if (current.Generation != expectedGeneration) return false;
            throw new InvalidOperationException("Failed to read a native calibration cache entry.");
        }

        private static CalibrationSharedCacheStatistics ToManaged(CalibrationCacheStatsV1 statistics)
            => new(
                statistics.EntryCount,
                statistics.Generation,
                statistics.EstimatedMemoryBytes,
                statistics.BudgetBytes,
                statistics.HitCount,
                statistics.MissCount);

        private static void EnsureCalibrationCacheResult(int result, string operation)
        {
            if (result != CalibrationOk)
            {
                throw new InvalidOperationException($"{operation} failed with native error code {result}.");
            }
        }

        private static NotSupportedException CreateCalibrationCacheCompatibilityException(Exception innerException)
            => new(
                "The installed opencv_helper.dll does not support calibration shared-cache management. " +
                "Deploy the matching native binary and retry.",
                innerException);
    }
}
