#pragma warning disable
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
namespace ColorVision.Common.NativeMethods
{
    public static class FileAssociation
    {
        [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint AssocQueryString(
            AssocF flags,
            AssocStr str,
            string pszAssoc,
            string pszExtra,
            [Out] StringBuilder pszOut,
            ref uint pcchOut
        );

        private enum AssocF : uint { None = 0 }
        private enum AssocStr : uint { FriendlyAppName = 4 }

        // 线程安全缓存
        private static readonly ConcurrentDictionary<string, string> _cache = new();

        /// <summary>
        /// 获取与扩展名或文件路径关联的友好应用程序名（带缓存）
        /// </summary>
        /// <param name="extOrPath">扩展名（如".txt"）或完整文件路径</param>
        /// <returns>默认打开方式的应用程序友好名称，若获取失败则返回 null</returns>
        public static string GetFriendlyAppName(string extOrPath)
        {
            if (string.IsNullOrWhiteSpace(extOrPath))
                throw new ArgumentNullException(nameof(extOrPath));

            // 先查缓存
            if (_cache.TryGetValue(extOrPath, out var cached))
                return cached;

            uint length = 0;
            uint ret = AssocQueryString(AssocF.None, AssocStr.FriendlyAppName, extOrPath, null, null, ref length);

            if (length == 0 || (ret != 1 && ret != 0)) // S_FALSE=1, S_OK=0
            {
                _cache[extOrPath] = null;
                return null;
            }

            var sb = new StringBuilder((int)length);
            ret = AssocQueryString(AssocF.None, AssocStr.FriendlyAppName, extOrPath, null, sb, ref length);

            if (ret != 0) // S_OK=0
            {
                _cache[extOrPath] = null;
                return null;
            }

            string result = sb.ToString();
            _cache[extOrPath] = result;
            return result;
        }
    }
}