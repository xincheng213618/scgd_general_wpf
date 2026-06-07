using System;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Filters
{
    internal sealed class DisplayShaderFilterEnvironment
    {
        private const int SM_REMOTESESSION = 0x1000;
        private static readonly Lazy<DisplayShaderFilterEnvironment> LazyCurrent = new(Create);

        public static DisplayShaderFilterEnvironment Current => LazyCurrent.Value;

        public int RenderTier { get; private init; }
        public bool IsRemoteSession { get; private init; }
        public bool SupportsPixelShader20 { get; private init; }
        public bool SupportsPixelShader30 { get; private init; }
        public bool SupportsPixelShader20InSoftware { get; private init; }
        public bool SupportsPixelShader30InSoftware { get; private init; }

        public bool CanUseShaderFilter => SupportsPixelShader20 || SupportsPixelShader20InSoftware;

        public bool ShouldShowNotice =>
            IsRemoteSession ||
            RenderTier < 2 ||
            !CanUseShaderFilter ||
            !SupportsPixelShader30;

        public string CreateNoticeText()
        {
            string headline;
            if (!CanUseShaderFilter)
            {
                headline = "The display shader filter cannot be enabled because this environment does not support WPF PixelShader 2.0.";
            }
            else if (IsRemoteSession)
            {
                headline = "Remote Desktop (mstsc) session detected. WPF shader support can change in remote sessions, so image filter behavior may differ from the local console.";
            }
            else if (RenderTier < 2)
            {
                headline = "The current WPF render tier is low. Display shader filters may be unavailable or slower than expected.";
            }
            else
            {
                headline = "The current environment does not report PixelShader 3.0 support. The display filter will use PixelShader 2.0 compatible shaders.";
            }

            return string.Join(
                Environment.NewLine,
                headline,
                string.Empty,
                $"Render tier: {RenderTier}",
                $"Remote Desktop: {FormatBool(IsRemoteSession)}",
                $"PixelShader 2.0: {FormatBool(SupportsPixelShader20)}",
                $"PixelShader 2.0 software: {FormatBool(SupportsPixelShader20InSoftware)}",
                $"PixelShader 3.0: {FormatBool(SupportsPixelShader30)}",
                $"PixelShader 3.0 software: {FormatBool(SupportsPixelShader30InSoftware)}");
        }

        private static DisplayShaderFilterEnvironment Create()
        {
            return new DisplayShaderFilterEnvironment
            {
                RenderTier = RenderCapability.Tier >> 16,
                IsRemoteSession = IsRemoteDesktopSession(),
                SupportsPixelShader20 = RenderCapability.IsPixelShaderVersionSupported(2, 0),
                SupportsPixelShader30 = RenderCapability.IsPixelShaderVersionSupported(3, 0),
                SupportsPixelShader20InSoftware = RenderCapability.IsPixelShaderVersionSupportedInSoftware(2, 0),
                SupportsPixelShader30InSoftware = RenderCapability.IsPixelShaderVersionSupportedInSoftware(3, 0),
            };
        }

        private static string FormatBool(bool value)
        {
            return value ? "Yes" : "No";
        }

        private static bool IsRemoteDesktopSession()
        {
            try
            {
                return GetSystemMetrics(SM_REMOTESESSION) != 0;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
    }
}
