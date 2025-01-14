using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Common.Utilities
{
    internal static class MemorySize
    {

        //所给路径中所对应的文件大小
        public static long FileSize(string filePath)
        {
            //定义一个FileInfo对象，是指与filePath所指向的文件相关联，以获取其大小
            FileInfo fileInfo = new(filePath);
            return fileInfo.Length;
        }

        /// <summary>
        /// 获取指定路径的大小
        /// </summary>
        /// <param name="dirPath">路径</param>
        /// <returns></returns>
        public static long GetDirectoryLength(string dirPath)
        {
            long len = 0;
            //判断该路径是否存在（是否为文件夹）
            if (!Directory.Exists(dirPath))
            {
                //查询文件的大小
                len = FileSize(dirPath);
            }
            else
            {
                //定义一个DirectoryInfo对象
                DirectoryInfo di = new(dirPath);

                //通过GetFiles方法，获取di目录中的所有文件的大小
                foreach (FileInfo fi in di.GetFiles())
                {
                    len += fi.Length;
                }
                //获取di中所有的文件夹，并存到一个新的对象数组中，以进行递归
                DirectoryInfo[] dis = di.GetDirectories();

                foreach (var item in dis)
                {
                    len += GetDirectoryLength(item.FullName);

                }
            }
            return len;
        }

        /// <summary>
        /// 获取指定路径的大小
        /// </summary>
        /// <param name="dirPath">路径</param>
        /// <returns></returns>
        public static long GetDirectoryLength(string dirPath, string removename)
        {
            long len = 0;
            //判断该路径是否存在（是否为文件夹）
            if (!Directory.Exists(dirPath))
            {
                //查询文件的大小
                len = FileSize(dirPath);
            }
            else
            {
                //定义一个DirectoryInfo对象
                DirectoryInfo di = new(dirPath);

                //通过GetFiles方法，获取di目录中的所有文件的大小
                foreach (FileInfo fi in di.GetFiles())
                {
                    len += fi.Length;
                }
                //获取di中所有的文件夹，并存到一个新的对象数组中，以进行递归
                DirectoryInfo[] dis = di.GetDirectories();
                foreach (var item in dis)
                {
                    if (item.Name != removename)
                        len += GetDirectoryLength(item.FullName, removename);
                }
            }
            return len;
        }
        public static bool TryParseMemorySize(string input, out long memorySize)
        {
            input = input.Trim().ToUpperInvariant();
            memorySize = 0;

            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            var units = new Dictionary<string, long>
            {
            { "PB", 1024L * 1024 * 1024 * 1024 * 1024 },
            { "P", 1024L * 1024 * 1024 * 1024 * 1024 },
            { "TB", 1024L * 1024 * 1024 * 1024 },
            { "T", 1024L * 1024* 1024  * 1024},
            { "GB", 1024L * 1024 * 1024 },
            { "G", 1024L * 1024* 1024 },
            { "MB", 1024L * 1024 },
            { "M", 1024L * 1024 }, // Adding "M" for MB
            { "KB", 1024L },
            { "K", 1024L * 1024 },
            { "B", 1L }
            };

            foreach (var unit in units)
            {
                if (input.EndsWith(unit.Key, StringComparison.CurrentCulture))
                {
                    if (double.TryParse(input.AsSpan(0, input.Length - unit.Key.Length), out double value))
                    {
                        memorySize = (long)(value * unit.Value);
                        return true;
                    }
                }
            }
            return long.TryParse(input, out memorySize); // Try to parse as bytes if no unit is found
        }

        public static string MemorySizeText(long memorySize)
        {
            // Define unit sizes for better readability and maintainability
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            const long TB = GB * 1024;
            const long PB = TB * 1024;

            // Tuple array to hold unit values and their corresponding names
            var units = new[] {
                Tuple.Create(PB, "PB"),
                Tuple.Create(TB, "TB"),
                Tuple.Create(GB, "GB"),
                Tuple.Create(MB, "MB"),
                Tuple.Create(KB, "kB"),
                Tuple.Create(1L, "Byte")
            };

            foreach (var unit in units)
            {
                if (memorySize >= unit.Item1)
                {
                    double value = (double)memorySize / unit.Item1;
                    // Check if we need to format the value to one decimal place
                    if (memorySize < unit.Item1 * 10)
                    {
                        return $"{value:F1} {unit.Item2}";
                    }
                    return $"{(long)value} {unit.Item2}";
                }
            }
            return "0 Byte"; // In case memorySize is 0
        }
    }
}

