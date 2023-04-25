using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.NativeMethods
{
    public static class Shlwapi
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        public static int CompareLogical(string str1, string str2) => StrCmpLogicalW(str1, str2);

    }
}
