#pragma warning disable CA1707,CA1711,CA1712,CA1401,CA1051,CA2101,CA1838,CA1806
using System.Runtime.InteropServices;

namespace ColorVision.Common.NativeMethods
{
    public static class Shlwapi
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        public static int CompareLogical(string str1, string str2) => StrCmpLogicalW(str1, str2);


    }
}
