using cvColorVision;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ColorVision.UI.Tests;

public class NativeCameraErrorTests
{
    [Fact]
    public void EveryCameraImportExistsInTheDeliveredDll()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "cvCamera.dll");
        IntPtr library = NativeLibrary.Load(path);
        try
        {
            foreach (MethodInfo method in typeof(cvCameraCSLib).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                var import = method.GetCustomAttribute<DllImportAttribute>();
                if (import?.Value != "cvCamera.dll") continue;
                Assert.True(NativeLibrary.TryGetExport(library, import.EntryPoint ?? method.Name, out _), $"Missing camera export: {import.EntryPoint ?? method.Name}");
            }
        }
        finally { NativeLibrary.Free(library); }
    }

    [Fact]
    public void AutoExposureFrameRejectsInvalidHandleWithoutWritingOutputs()
    {
        uint width = 1, height = 2, bpp = 16, channels = 3;
        float[] exposure = [12, 23, 34], saturation = [70, 71, 72];
        Assert.Equal(-10000, cvCameraCSLib.CM_GetAutoExpFrame(IntPtr.Zero, ref width, ref height, ref bpp, ref channels, IntPtr.Zero, 0, exposure, saturation));
        Assert.Equal(1u, width);
        Assert.Equal(new float[] { 12, 23, 34 }, exposure);
        Assert.Equal(new float[] { 70, 71, 72 }, saturation);
    }

    [Fact]
    public void CameraStructuresMatchReleaseX64NativeHeaderLayout()
    {
        // sizeof/offsetof from a Release/x64 C++ probe including the actual CMStruct.h.
        Assert.Equal(8, IntPtr.Size);
        AssertLayout<CVImage>(24, [nameof(CVImage.nWidth), nameof(CVImage.nHeight), nameof(CVImage.nChannels), nameof(CVImage.nBpp), nameof(CVImage.pData)], [0, 4, 8, 12, 16]);
        AssertLayout<CRECT>(16, [nameof(CRECT.x), nameof(CRECT.y), nameof(CRECT.cx), nameof(CRECT.cy)], [0, 4, 8, 12]);
        AssertLayout<IRECT>(16, [nameof(IRECT.x), nameof(IRECT.y), nameof(IRECT.cx), nameof(IRECT.cy)], [0, 4, 8, 12]);
        AssertLayout<AutoFocusCfg>(48, [nameof(AutoFocusCfg.forwardparam), nameof(AutoFocusCfg.curtailparam), nameof(AutoFocusCfg.curStep), nameof(AutoFocusCfg.stopStep), nameof(AutoFocusCfg.minPosition), nameof(AutoFocusCfg.maxPosition), nameof(AutoFocusCfg.eEvaFunc), nameof(AutoFocusCfg.dMinValue)], [0, 8, 16, 20, 24, 28, 32, 40]);
        AssertLayout<cvCameraCSLib.ChromaInfo>(44, ["fX", "fY", "fZ", "fx", "fy", "fu", "fv", "fCCT", "fWave", "nMidPointX", "nMidPointY"], [0, 4, 8, 12, 16, 20, 24, 28, 32, 36, 40]);
    }

    [Fact]
    public void NativeFrameAndFocusCallbacksReturnInt32UsingStdCall()
    {
        Type[] callbacks = [typeof(cvCameraCSLib.QHYCCDProcCallBack), typeof(cvCameraCSLib.AutoFocusCallBack)];
        Assert.All(callbacks, callback =>
        {
            Assert.Equal(typeof(int), callback.GetMethod("Invoke")!.ReturnType);
            Assert.Equal(CallingConvention.StdCall, callback.GetCustomAttribute<UnmanagedFunctionPointerAttribute>()!.CallingConvention);
        });
    }

    [Fact]
    public void StringHelpersDoNotTurnInvalidHandleErrorsIntoEmptySuccess()
    {
        Assert.Contains("-10000", Assert.Throws<InvalidOperationException>(() => cvCameraCSLib.GetCfgToJson(IntPtr.Zero, ConfigType.Cfg_Camera, false)).Message);
        Assert.Contains("-10000", Assert.Throws<InvalidOperationException>(() => cvCameraCSLib.CM_GetSN(IntPtr.Zero)).Message);
        Assert.Contains("-10000", Assert.Throws<InvalidOperationException>(() => cvCameraCSLib.CM_GetDeviceMode(IntPtr.Zero)).Message);
    }

    private static void AssertLayout<T>(int size, string[] fields, int[] offsets) where T : struct
    {
        Assert.Equal(size, Marshal.SizeOf<T>());
        for (int index = 0; index < fields.Length; index++) Assert.Equal(offsets[index], Marshal.OffsetOf<T>(fields[index]).ToInt32());
    }

    [Theory]
    [InlineData(0x80000206u, "MV_E_NETER")]
    [InlineData(0x80000004u, "MV_E_PARAMETER")]
    [InlineData(0x80000203u, "MV_E_ACCESS_DENIED")]
    public async Task MvsCodeCanBeExplainedOnAnotherThreadWithoutCameraOrLastError(uint sdkCode, string name)
    {
        string message = await Task.Run(() => GetMessage(unchecked((int)sdkCode)));

        Assert.Contains(name, message);
        Assert.Contains($"0x{sdkCode:X8}", message);
        Assert.DoesNotContain("SN=", message);
    }

    [Fact]
    public void UnknownMvsCodeStillReturnsOriginalHexadecimalValue()
    {
        Assert.Contains("0x8000FFFF", GetMessage(unchecked((int)0x8000FFFFu)));
        Assert.Contains("MVS", GetMessage(unchecked((int)0x8000FFFFu)));
    }

    [Fact]
    public void ExposureAndGainPreserveIntegerFailureCodes()
    {
        // Existing native platform codes from cvErrorDefine.h are not mirrored
        // in the small managed cvErrorDefine class.
        Assert.Equal(-10000, cvCameraCSLib.CM_SetExpTime(IntPtr.Zero, 10));
        Assert.Equal(-10000, cvCameraCSLib.CM_SetGain(IntPtr.Zero, 1));
        Assert.NotEmpty(GetMessage(-10048));
        Assert.NotEmpty(GetMessage(-10038));
    }

    [Fact]
    public void CameraOperationImportsReserveIntegerErrorCodes()
    {
        // Only actual predicates may discard the numeric result at the DLL
        // boundary. Future operations must preserve errors, including negatives.
        string[] predicates = ["CM_IsOpen", "CM_GetDeviceOnline", "CM_IsFeatureAvailable_Gen", "CM_IsBurstmodeAvailable"];
        var booleanImports = typeof(cvCameraCSLib).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<DllImportAttribute>() != null && method.ReturnType == typeof(bool));
        Assert.All(booleanImports, method => Assert.Contains(method.Name, predicates));
    }

    [Fact]
    public void OtherCameraOperationsPreserveInvalidHandleErrors()
    {
        const int invalidHandle = -10000;
        Assert.Equal(invalidHandle, cvCameraCSLib.CM_SetTakeImageMode(IntPtr.Zero, TakeImageMode.Measure_Normal));
        Assert.Equal(invalidHandle, cvCameraCSLib.CM_SetImageBpp(IntPtr.Zero, 8));
        Assert.Equal(invalidHandle, cvCameraCSLib.CM_SetExpTimeEx(IntPtr.Zero, 0, 10));
        Assert.Equal(invalidHandle, cvCameraCSLib.CM_SetParam(IntPtr.Zero, 0, 0));
        Assert.Equal(invalidHandle, cvCameraCSLib.CM_SetPort(IntPtr.Zero, 0));
        Assert.Equal(invalidHandle, cvCameraCSLib.UpdateCfgJson(IntPtr.Zero, ConfigType.Cfg_Camera, "{}"));
        Assert.Equal(invalidHandle, cvCameraCSLib.CM_SetCallBack(IntPtr.Zero, (_, _, _, _, _, _, _, _) => 0, IntPtr.Zero));
        Assert.Equal(invalidHandle, cvCameraCSLib.CM_UnregisterCallBack(IntPtr.Zero));
        uint channels = 0;
        Assert.Equal(invalidHandle, cvCameraCSLib.CM_GetChannels(IntPtr.Zero, ref channels));
        int position = 0;
        Assert.Equal(invalidHandle, cvCameraCSLib.CM_GetAutoFocus(IntPtr.Zero, ref position));
        Assert.Equal(invalidHandle, cvCameraCSLib.IsOpen(IntPtr.Zero));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceFrameHelpersReturnInfoFailureBeforeAllocating(bool extended)
    {
        uint width = uint.MaxValue, height = uint.MaxValue, bits = 16, channels = 3;
        byte[] buffer = [1];
        int result = extended
            ? cvCameraCSLib.CM_GetSrcFrameEx(IntPtr.Zero, ref width, ref height, ref bits, ref channels, ref buffer)
            : cvCameraCSLib.CM_GetSrcFrame(IntPtr.Zero, ref width, ref height, ref bits, ref channels, ref buffer);

        Assert.Equal(-10000, result);
        Assert.Empty(buffer);
    }

    [Fact]
    public void InvalidHandlesNeverReportOpenOrSupportedState()
    {
        Assert.False(cvCameraCSLib.CM_IsOpen(IntPtr.Zero));
        Assert.False(cvCameraCSLib.CM_GetDeviceOnline(IntPtr.Zero));
        Assert.False(cvCameraCSLib.CM_IsFeatureAvailable(IntPtr.Zero, 0));
        Assert.False(cvCameraCSLib.CM_IsBurstmodeAvailable(IntPtr.Zero));
    }

    [Fact]
    public void InsufficientMessageBufferReportsRequiredSizeWithoutWritingPastCapacity()
    {
        int code = unchecked((int)0x80000206u);
        var message = new StringBuilder(1);
        int length = 1;

        Assert.Equal(0, cvCameraCSLib.CM_GetErrorMessage(code, message, ref length));
        Assert.True(length > 1);
        Assert.Empty(message.ToString());

        message = new StringBuilder(length);
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, cvCameraCSLib.CM_GetErrorMessage(code, message, ref length));
        Assert.Contains("MV_E_NETER", message.ToString());
    }

    private static string GetMessage(int code)
    {
        string message = string.Empty;
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, cvCameraCSLib.CM_GetErrorMessage(code, ref message));
        return message;
    }
}
