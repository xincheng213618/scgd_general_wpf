#pragma warning disable CS0169
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Spectrum.DirectSpectrometer;

internal static class SpectrometerApi
{
    private const string DllName = "SpectraArsenal.dll";
    private static readonly object NativeLoadLock = new();
    private static bool _nativeDependenciesLoaded;

    public static IReadOnlyList<string> RequiredNativeFiles { get; } = new[]
    {
        DllName,
        "libusb0.dll",
        "msvcr100.dll"
    };

    static SpectrometerApi()
    {
        NativeLibrary.SetDllImportResolver(typeof(SpectrometerApi).Assembly, ResolveNativeLibrary);
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SA_GetAPIVersion();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SA_OpenSpectrometers();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SA_CloseSpectrometers();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SA_SetIntegrationTime(int spectrometerIndex, int usec);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SA_SetAverageTimes(int spectrometerIndex, int averageTimes);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SA_GetWavelength(int spectrometerIndex, [Out] double[] wavelengthData, ref int spectrumNumber);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SA_GetSpectum(int spectrometerIndex, [Out] double[] spectrumData, ref int spectrumNumber);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SA_NonlinearCalibration(int spectrometerIndex, double[] spectrumData, [Out] double[] newSpectrumData, int spectrumNumber);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SA_GetSerialNumber(int spectrometerIndex);

    public static string GetApiVersion()
    {
        return PtrToString(SA_GetAPIVersion());
    }

    public static string GetSerialNumber(int spectrometerIndex)
    {
        return PtrToString(SA_GetSerialNumber(spectrometerIndex));
    }


    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals(DllName, StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }
        var assemblyDirectory = Path.GetDirectoryName(typeof(SpectrometerApi).Assembly.Location);
        var baseDirectory = AppContext.BaseDirectory;
        var dllPath = Path.Combine(baseDirectory, DllName);
        return NativeLibrary.Load(dllPath);
    }


    private static string PtrToString(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
    }
}