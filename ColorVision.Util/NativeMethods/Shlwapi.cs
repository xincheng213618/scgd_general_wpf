using System.Runtime.InteropServices;

namespace ColorVision.NativeMethods
{
    public static class Shlwapi
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        public static int CompareLogical(string str1, string str2) => StrCmpLogicalW(str1, str2);

    }
}
