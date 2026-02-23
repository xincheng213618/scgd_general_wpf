using System;
using System.Runtime.InteropServices;

namespace ColorVision.ShellExtension.Interop
{
    /// <summary>
    /// Windows Shell IThumbnailProvider COM interface.
    /// Explorer calls GetThumbnail to obtain an HBITMAP for the file preview.
    /// GUID: E357FCCD-A995-4576-B01F-234630154E96
    /// </summary>
    [ComImport]
    [Guid("E357FCCD-A995-4576-B01F-234630154E96")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellThumbnailProvider
    {
        /// <summary>
        /// Gets a thumbnail image for the file.
        /// </summary>
        /// <param name="cx">Maximum thumbnail dimension (width or height) in pixels.</param>
        /// <param name="phbmp">Receives the HBITMAP handle. Caller takes ownership.</param>
        /// <param name="pdwAlpha">Receives the alpha type (WTS_ALPHATYPE).</param>
        /// <returns>HRESULT: S_OK on success, error code on failure.</returns>
        [PreserveSig]
        int GetThumbnail(uint cx, out IntPtr phbmp, out uint pdwAlpha);
    }

    /// <summary>
    /// COM interface for initializing a handler with a file path.
    /// Explorer calls Initialize before GetThumbnail to provide the file path.
    /// GUID: B7D14566-0509-4CCE-A71F-0A554233BD9B
    /// </summary>
    [ComImport]
    [Guid("B7D14566-0509-4CCE-A71F-0A554233BD9B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInitializeWithFile
    {
        /// <summary>
        /// Initializes the handler with a file path.
        /// </summary>
        /// <param name="pszFilePath">The file path.</param>
        /// <param name="grfMode">The access mode (STGM values).</param>
        /// <returns>HRESULT: S_OK on success.</returns>
        [PreserveSig]
        int Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
    }

    /// <summary>
    /// COM interface for initializing a handler with a stream.
    /// GUID: B824B49D-22AC-4161-AC8A-9916E8FA3F7F
    /// </summary>
    [ComImport]
    [Guid("B824B49D-22AC-4161-AC8A-9916E8FA3F7F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public interface IInitializeWithStream
#pragma warning restore CA1711
    {
        /// <summary>
        /// Initializes the handler with a stream.
        /// </summary>
        /// <param name="pstream">Pointer to the IStream interface.</param>
        /// <param name="grfMode">The access mode (STGM values).</param>
        /// <returns>HRESULT: S_OK on success.</returns>
        [PreserveSig]
        int Initialize(System.Runtime.InteropServices.ComTypes.IStream pstream, uint grfMode);
    }

    /// <summary>
    /// WTS_ALPHATYPE constants for IThumbnailProvider.GetThumbnail pdwAlpha parameter.
    /// Names follow the Windows SDK convention.
    /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores
    public static class WtsAlphaType
    {
        public const uint WTSAT_UNKNOWN = 0;
        public const uint WTSAT_RGB = 1;
        public const uint WTSAT_ARGB = 2;
    }
#pragma warning restore CA1707
}
