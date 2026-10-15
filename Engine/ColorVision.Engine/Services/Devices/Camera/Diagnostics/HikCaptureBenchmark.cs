extern alias HikMvsSdk;
using MyCamera = HikMvsSdk::MvCamCtrl.NET.MyCamera;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Diagnostics;

internal sealed record HikTestDevice(string Serial, string Model, string Transport, MyCamera.MV_CC_DEVICE_INFO Native)
{
    public override string ToString() => $"{Model} · {Serial} · {Transport}";
}

internal sealed record HikCaptureSample(int Run, int Index, double SetupMs, double CaptureMs,
    double WaitMs, double ConvertMs, double ThreadCpuMs, uint FrameId, uint Width, uint Height, float ActualExposureUs, float ActualGain,
    string? RawExportPath = null)
{
    public bool FirstFrame => Index == 1;
    public double CycleMs => SetupMs + CaptureMs;
}

// Used only in the --hik-capture-test process. No DeviceCamera, database, cache or flow state is involved.
internal static class HikCaptureBenchmark
{
    private static bool sdkInitialized;

    internal static IReadOnlyList<HikTestDevice> Discover()
    {
        EnsureSdk();
        MyCamera.MV_CC_DEVICE_INFO_LIST list = new();
        Check(MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref list), "EnumDevices");
        var devices = new List<HikTestDevice>();
        for (int i = 0; i < list.nDeviceNum; i++)
        {
            var info = Marshal.PtrToStructure<MyCamera.MV_CC_DEVICE_INFO>(list.pDeviceInfo[i]);
            if (info.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                var detail = (MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(info.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO));
                devices.Add(new(detail.chSerialNumber.TrimEnd('\0'), detail.chModelName.TrimEnd('\0'), "GigE", info));
            }
            else if (info.nTLayerType == MyCamera.MV_USB_DEVICE)
            {
                var detail = (MyCamera.MV_USB3_DEVICE_INFO)MyCamera.ByteToStruct(info.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO));
                devices.Add(new(detail.chSerialNumber.TrimEnd('\0'), detail.chModelName.TrimEnd('\0'), "USB3", info));
            }
        }
        return devices;
    }

    internal static void Run(HikCaptureTestSettings settings, int run, BlockingCollection<HikCaptureRequest> captureRequests,
        IProgress<bool> ready, IProgress<HikCaptureSample> samples, IProgress<string> log, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(settings.CameraId) || !float.IsFinite(settings.ExposureMs) || settings.ExposureMs <= 0
            || settings.ExposureMs > 60000 || !float.IsFinite(settings.Gain) || settings.Gain < 0
            || settings.BayerQuality > 3)
            throw new InvalidOperationException(EngineLocalization.Get("HikTest_InvalidSettings"));
        EnsureSdk();
        cancellation.ThrowIfCancellationRequested();
        log.Report($"Run {run} · MVS · {DateTimeOffset.Now:O}\n"
            + $"SN={settings.CameraId}; exposure={settings.ExposureMs.ToString(CultureInfo.InvariantCulture)} ms; gain={settings.Gain.ToString(CultureInfo.InvariantCulture)}; manual single-frame; average=1; Bayer16 → RGB48\n"
            + $"MVS SDK=0x{MyCamera.MV_CC_GetSDKVersion_NET():X8}; Bayer quality={settings.BayerQuality}; process={Environment.ProcessId}");
        using var process = Process.GetCurrentProcess();
        foreach (ProcessModule module in process.Modules)
            if (module.ModuleName.Equals("MvCameraControl.dll", StringComparison.OrdinalIgnoreCase))
                log.Report($"{module.ModuleName}: {module.FileVersionInfo.FileVersion} · {module.FileName}");
        RunMvs(settings, run, captureRequests, ready, samples, log, cancellation);
    }

    private static void RunMvs(HikCaptureTestSettings settings, int run, BlockingCollection<HikCaptureRequest> captureRequests,
        IProgress<bool> ready, IProgress<HikCaptureSample> samples, IProgress<string> log, CancellationToken cancellation)
    {
        HikTestDevice device = Discover().SingleOrDefault(d => d.Serial.Equals(settings.CameraId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(EngineLocalization.Get("HikTest_NotFound"));
        var camera = new MyCamera();
        var info = device.Native;
        Check(camera.MV_CC_CreateDevice_NET(ref info), "CreateDevice");
        bool opened = false, grabbing = false;
        IntPtr output = IntPtr.Zero;
        var restore = new Stack<(string Name, Action Restore)>();
        var cleanupErrors = new List<Exception>();
        Exception? captureFailure = null;
        try
        {
            var opening = Stopwatch.StartNew();
            Check(camera.MV_CC_OpenDevice_NET(), "OpenDevice (exclusive)");
            opened = true;
            log.Report($"{device}; open={opening.Elapsed.TotalMilliseconds:F3} ms (excluded from frame timing)");
            LogCameraNodes(camera, log, "Before");

            MyCamera.MVCC_ENUMVALUE pixel = new();
            Check(camera.MV_CC_GetEnumValue_NET("PixelFormat", ref pixel), "Get PixelFormat");
            var formats = pixel.nSupportValue.Take((int)pixel.nSupportedNum).Select(v => (MyCamera.MvGvspPixelType)v).ToArray();
            var bayer16 = formats.Where(v => v.ToString().StartsWith("PixelType_Gvsp_Bayer", StringComparison.Ordinal)
                && v.ToString().EndsWith("16", StringComparison.Ordinal)).ToArray();
            string currentFormat = ((MyCamera.MvGvspPixelType)pixel.nCurValue).ToString();
            var source = bayer16.FirstOrDefault(v => currentFormat.StartsWith(v.ToString()[..^2], StringComparison.Ordinal));
            if ((uint)source == 0 && bayer16.Length == 1) source = bayer16[0];
            if ((uint)source == 0) throw new InvalidOperationException(EngineLocalization.Get("HikTest_Bayer16Required"));
            SetEnum("PixelFormat", (uint)source);
            ConfigureSoftwareTrigger();
            SetEnum("ExposureAuto", 0);
            SetEnum("GainAuto", 0);
            // CameraManager::SetExpTime truncates milliseconds to integer microseconds.
            int exposureUs = checked((int)(settings.ExposureMs * 1000));
            SetFloat("ExposureTime", exposureUs);
            SetFloat("Gain", settings.Gain);
            Check(camera.MV_CC_SetBayerCvtQuality_NET(settings.BayerQuality), "SetBayerCvtQuality");
            Check(camera.MV_CC_SetImageNodeNum_NET(2), "SetImageNodeNum");
            uint width = checked((uint)GetInt(camera, "Width")), height = checked((uint)GetInt(camera, "Height"));
            int length = OutputLength(width, height);
            output = Marshal.AllocHGlobal(length);
            string cameraSettings = LogCameraNodes(camera, log, "Capture");
            log.Report($"Conversion: {source} → RGB16_Packed (RGB48), quality={settings.BayerQuality}; destination={length} bytes; reused\n"
                + "ADC depth, ROI, black level, white balance and transport settings are read back below; no UserSetSave is used.");
            Check(camera.MV_CC_StartGrabbing_NET(), "StartGrabbing");
            grabbing = true;
            uint? lastFrame = null;
            ready.Report(true);
            for (int i = 1; ; i++)
            {
                // Keep the same connection and buffer between manual clicks. Idle time is not measured.
                HikCaptureRequest request = captureRequests.Take(cancellation);
                cancellation.ThrowIfCancellationRequested();
                var setup = Stopwatch.StartNew();
                // Match CameraManager's readback tolerances (0.05 gain / 200 us, 2 s deadline).
                float actualGain = SetAndVerifyFloat(camera, "Gain", settings.Gain, 0.05f, cancellation);
                float actualExposure = SetAndVerifyFloat(camera, "ExposureTime", exposureUs, 200, cancellation);
                Check(camera.MV_CC_ClearImageBuffer_NET(), "ClearImageBuffer");
                double setupMs = setup.Elapsed.TotalMilliseconds;
                MyCamera.MV_FRAME_OUT frame = new();
                double cpu = ThreadCpuMilliseconds();
                var capture = Stopwatch.StartNew();
                Check(camera.MV_CC_SetCommandValue_NET("TriggerSoftware"), "TriggerSoftware");
                int timeout = checked((int)Math.Max(5000, settings.ExposureMs + 3000));
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int code = camera.MV_CC_GetImageBuffer_NET(ref frame, 100);
                    if (code == MyCamera.MV_OK) break;
                    if (code != MyCamera.MV_E_NODATA) Check(code, "GetImageBuffer");
                    if (capture.ElapsedMilliseconds >= timeout) throw new TimeoutException("GetImageBuffer timed out.");
                }
                double waitMs = capture.Elapsed.TotalMilliseconds, convertMs;
                DateTimeOffset receivedAt = DateTimeOffset.Now;
                try
                {
                    var frameInfo = frame.stFrameInfo;
                    if (frameInfo.nWidth != width || frameInfo.nHeight != height || frameInfo.enPixelType != source
                        || frameInfo.nFrameLen < checked(width * height * 2) || frameInfo.nLostPacket != 0 || frameInfo.nFrameNum == lastFrame)
                        throw new InvalidOperationException("Incomplete, repeated or unexpected MVS frame; sample excluded.");
                    var conversion = new MyCamera.MV_CC_PIXEL_CONVERT_PARAM_EX
                    {
                        nWidth = width, nHeight = height, enSrcPixelType = source,
                        pSrcData = frame.pBufAddr, nSrcDataLen = frameInfo.nFrameLen,
                        enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB16_Packed,
                        pDstBuffer = output, nDstBufferSize = (uint)length
                    };
                    var converting = Stopwatch.StartNew();
                    int code = camera.MV_CC_ConvertPixelTypeEx_NET(ref conversion);
                    convertMs = converting.Elapsed.TotalMilliseconds;
                    Check(code, "ConvertPixelTypeEx");
                    if (conversion.nDstLen != length) throw new InvalidOperationException("Unexpected RGB48 output length.");
                    lastFrame = frameInfo.nFrameNum;
                    if (request.RawExportPath != null)
                    {
                        // Export only on explicit request; ordinary captures add no copy or retained frame.
                        // Pause timing, keep ownership until writing ends, then return the SDK buffer normally.
                        capture.Stop();
                        double exportCpu = ThreadCpuMilliseconds();
                        var exporting = Stopwatch.StartNew();
                        long imageBytes = checked((long)width * height * 2);
                        HikRawFrameExport.Save(request.RawExportPath, frame.pBufAddr, checked((int)frameInfo.nFrameLen), new
                        {
                            SchemaVersion = 1, RawFile = "frame.bayer16.raw", ReceivedAt = receivedAt,
                            CameraSerial = device.Serial, CameraModel = device.Model, device.Transport,
                            Run = run, Index = i, FrameId = lastFrame.Value, Width = width, Height = height,
                            PixelFormat = source.ToString(), PixelFormatCode = $"0x{(uint)source:X8}",
                            BayerPattern = source.ToString() switch
                            {
                                "PixelType_Gvsp_BayerRG16" => "RGGB", "PixelType_Gvsp_BayerGR16" => "GRBG",
                                "PixelType_Gvsp_BayerGB16" => "GBRG", "PixelType_Gvsp_BayerBG16" => "BGGR",
                                _ => "Unknown"
                            },
                            StorageBits = 16, Channels = 1, ByteOrder = "little-endian", HeaderBytes = 0,
                            ImageBytes = imageBytes, PayloadBytes = frameInfo.nFrameLen,
                            RowStrideBytes = frameInfo.nFrameLen == imageBytes ? (long?)width * 2 : null,
                            OffsetX = frameInfo.nOffsetX, OffsetY = frameInfo.nOffsetY, UnparsedChunks = frameInfo.nUnparsedChunkNum,
                            ExposureUs = actualExposure, Gain = actualGain, settings.BayerQuality,
                            SdkVersion = $"0x{MyCamera.MV_CC_GetSDKVersion_NET():X8}", CameraSettings = cameraSettings,
                            LayoutNote = "Exact SDK source payload, before Bayer-to-RGB interpolation; no calibration, flip or normalization. If RowStrideBytes is null, inspect payload/chunk layout before decoding."
                        }, cancellation);
                        log.Report(FormattableString.Invariant($"[HikRawExport] Run={run} Index={i} FrameId={lastFrame} Path={request.RawExportPath} PayloadBytes={frameInfo.nFrameLen} ExportMs={exporting.Elapsed.TotalMilliseconds:F3} (excluded from capture timing)"));
                        cpu += ThreadCpuMilliseconds() - exportCpu;
                        capture.Start();
                    }
                }
                finally { Check(camera.MV_CC_FreeImageBuffer_NET(ref frame), "FreeImageBuffer"); }
                double captureMs = capture.Elapsed.TotalMilliseconds;
                double cpuMs = ThreadCpuMilliseconds() - cpu;
                samples.Report(new(run, i, setupMs, captureMs, waitMs, convertMs, cpuMs, lastFrame!.Value, width, height, actualExposure, actualGain, request.RawExportPath));
            }
        }
        catch (Exception ex)
        {
            log.Report($"Capture: {ex.Message}");
            captureFailure = ex;
        }
        finally
        {
            // Continue closing even if a disconnected device cannot restore a volatile node.
            if (grabbing) Cleanup("StopGrabbing", () => Check(camera.MV_CC_StopGrabbing_NET(), "StopGrabbing"));
            while (restore.TryPop(out var entry)) Cleanup(entry.Name, entry.Restore);
            if (output != IntPtr.Zero) Marshal.FreeHGlobal(output);
            if (opened) Cleanup("CloseDevice", () => Check(camera.MV_CC_CloseDevice_NET(), "CloseDevice"));
            Cleanup("DestroyDevice", () => Check(camera.MV_CC_DestroyDevice_NET(), "DestroyDevice"));
        }
        if (cleanupErrors.Count > 0)
        {
            if (captureFailure != null) cleanupErrors.Insert(0, captureFailure);
            throw new AggregateException("Camera cleanup failed. See the diagnostic report.", cleanupErrors);
        }
        if (captureFailure != null) ExceptionDispatchInfo.Capture(captureFailure).Throw();

        void Cleanup(string name, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                string message = $"Cleanup {name}: {ex.Message}";
                cleanupErrors.Add(ex);
                log.Report(message);
            }
        }
        void SetEnum(string key, uint value)
        {
            MyCamera.MVCC_ENUMVALUE old = new();
            Check(camera.MV_CC_GetEnumValue_NET(key, ref old), $"Get {key}");
            uint previous = old.nCurValue;
            if (previous == value) return;
            restore.Push((key, () => Check(camera.MV_CC_SetEnumValue_NET(key, previous), $"Restore {key}")));
            Check(camera.MV_CC_SetEnumValue_NET(key, value), $"Set {key}");
            MyCamera.MVCC_ENUMVALUE actual = new();
            Check(camera.MV_CC_GetEnumValue_NET(key, ref actual), $"Read back {key}");
            if (actual.nCurValue != value)
                throw new InvalidOperationException($"{key}: requested={value}, actual={actual.nCurValue}");
        }
        void ConfigureSoftwareTrigger()
        {
            MyCamera.MVCC_ENUMVALUE selectors = new();
            Check(camera.MV_CC_GetEnumValue_NET("TriggerSelector", ref selectors), "Get TriggerSelector");
            if (selectors.nSupportedNum == 0 || selectors.nSupportValue == null || selectors.nSupportedNum > selectors.nSupportValue.Length)
                throw new InvalidOperationException("MVS returned invalid trigger selectors.");
            uint[] supported = selectors.nSupportValue.Take((int)selectors.nSupportedNum).ToArray();
            // GenICam selector values: FrameStart=0, FrameBurstStart=6.
            uint target = selectors.nCurValue;
            if ((target != 0 && target != 6) || !supported.Contains(target))
            {
                if (supported.Contains(0u)) target = 0;
                else if (supported.Contains(6u)) target = 6;
                else throw new InvalidOperationException("MVS camera has no supported frame trigger selector.");
            }
            // Match production: one software trigger produces one frame, regardless of saved device settings.
            foreach (uint selector in supported)
            {
                SetEnum("TriggerSelector", selector);
                SetEnum("TriggerMode", 0);
            }
            SetEnum("AcquisitionMode", 2);
            SetEnum("TriggerSelector", target);
            MyCamera.MVCC_INTVALUE_EX burst = new();
            int burstCode = camera.MV_CC_GetIntValueEx_NET("AcquisitionBurstFrameCount", ref burst);
            if (burstCode == MyCamera.MV_OK)
            {
                if (burst.nCurValue != 1)
                {
                    long previous = burst.nCurValue;
                    restore.Push(("AcquisitionBurstFrameCount", () => Check(camera.MV_CC_SetIntValueEx_NET("AcquisitionBurstFrameCount", previous), "Restore AcquisitionBurstFrameCount")));
                    Check(camera.MV_CC_SetIntValueEx_NET("AcquisitionBurstFrameCount", 1), "Set AcquisitionBurstFrameCount");
                }
                if (GetInt(camera, "AcquisitionBurstFrameCount") != 1)
                    throw new InvalidOperationException("MVS AcquisitionBurstFrameCount readback is not one.");
            }
            else if (target == 6 || (burstCode != MyCamera.MV_E_SUPPORT && burstCode != MyCamera.MV_E_GC_GENERIC))
                Check(burstCode, "Get AcquisitionBurstFrameCount");
            SetEnum("TriggerSource", 7);
            SetEnum("TriggerMode", 1);
            log.Report("Software trigger configured for this session; one frame per manual capture; original settings restored on close.");
        }
        void SetFloat(string key, float value)
        {
            MyCamera.MVCC_FLOATVALUE old = new();
            Check(camera.MV_CC_GetFloatValue_NET(key, ref old), $"Get {key}");
            Check(camera.MV_CC_SetFloatValue_NET(key, value), $"Set {key}");
            float previous = old.fCurValue;
            restore.Push((key, () => Check(camera.MV_CC_SetFloatValue_NET(key, previous), $"Restore {key}")));
        }
    }

    private static string LogCameraNodes(MyCamera camera, IProgress<string> log, string phase)
    {
        var values = new List<string>();
        foreach (string name in new[] { "Width", "Height", "OffsetX", "OffsetY", "PayloadSize", "DeviceLinkSpeed", "DeviceLinkCurrentThroughput", "GevSCPSPacketSize", "AcquisitionBurstFrameCount" })
        {
            MyCamera.MVCC_INTVALUE_EX value = new();
            int code = camera.MV_CC_GetIntValueEx_NET(name, ref value);
            values.Add($"{name}={(code == 0 ? value.nCurValue.ToString(CultureInfo.InvariantCulture) : $"N/A(0x{code:X8})")}");
        }
        foreach (string name in new[] { "ExposureTime", "Gain", "ResultingFrameRate", "AcquisitionFrameRate", "Gamma", "BlackLevel", "BalanceRatio" })
        {
            MyCamera.MVCC_FLOATVALUE value = new();
            int code = camera.MV_CC_GetFloatValue_NET(name, ref value);
            values.Add($"{name}={(code == 0 ? value.fCurValue.ToString(CultureInfo.InvariantCulture) : $"N/A(0x{code:X8})")}");
        }
        foreach (string name in new[] { "PixelFormat", "ADCBitDepth", "BalanceWhiteAuto", "BalanceRatioSelector", "ExposureAuto", "GainAuto", "AcquisitionMode", "TriggerSelector", "TriggerMode", "TriggerSource" })
        {
            MyCamera.MVCC_ENUMVALUE value = new();
            int code = camera.MV_CC_GetEnumValue_NET(name, ref value);
            var entry = new MyCamera.MVCC_ENUMENTRY { nValue = value.nCurValue };
            string symbolic = code == 0 && camera.MV_CC_GetEnumEntrySymbolic_NET(name, ref entry) == 0
                ? Encoding.ASCII.GetString(entry.chSymbolic).TrimEnd('\0') : $"0x{value.nCurValue:X8}";
            values.Add($"{name}={(code == 0 ? symbolic : $"N/A(0x{code:X8})")}");
        }
        foreach (string name in new[] { "AcquisitionFrameRateEnable", "GammaEnable", "ReverseX", "ReverseY" })
        {
            bool value = false;
            int code = camera.MV_CC_GetBoolValue_NET(name, ref value);
            values.Add($"{name}={(code == 0 ? value.ToString() : $"N/A(0x{code:X8})")}");
        }
        string settings = string.Join("; ", values);
        log.Report($"{phase}: {settings}");
        return settings;
    }

    private static long GetInt(MyCamera camera, string key)
    {
        MyCamera.MVCC_INTVALUE_EX value = new();
        Check(camera.MV_CC_GetIntValueEx_NET(key, ref value), $"Get {key}");
        return value.nCurValue;
    }

    private static float SetAndVerifyFloat(MyCamera camera, string key, float requested, float tolerance, CancellationToken cancellation)
    {
        Check(camera.MV_CC_SetFloatValue_NET(key, requested), $"Set {key}");
        var wait = Stopwatch.StartNew();
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            MyCamera.MVCC_FLOATVALUE actual = new();
            Check(camera.MV_CC_GetFloatValue_NET(key, ref actual), $"Read back {key}");
            if (Math.Abs(actual.fCurValue - requested) <= tolerance) return actual.fCurValue;
            if (wait.ElapsedMilliseconds >= 2000)
                throw new TimeoutException($"{key}: requested={requested}, actual={actual.fCurValue}");
            Thread.Sleep(1);
        }
    }

    internal static int OutputLength(uint width, uint height)
    {
        if (width == 0 || height == 0) throw new InvalidOperationException("Empty image dimensions.");
        return checked((int)(checked((long)width * height * 6)));
    }

    private static void EnsureSdk()
    {
        if (sdkInitialized) return;
        if (!Environment.Is64BitProcess) throw new InvalidOperationException("MVS capture test requires x64.");
        // MVS also serves native cvCamera sessions. Keep its process-wide runtime alive when this window closes.
        Check(MyCamera.MV_CC_Initialize_NET(), "Initialize MVS SDK");
        sdkInitialized = true;
    }

    private static void Check(int code, string operation)
    {
        if (code != 0) throw new InvalidOperationException($"{operation}: 0x{code:X8}");
    }

    private static double ThreadCpuMilliseconds()
    {
        return GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user) ? (kernel + user) / 10000d : double.NaN;
    }
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);
}
