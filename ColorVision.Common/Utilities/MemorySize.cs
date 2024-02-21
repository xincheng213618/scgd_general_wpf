using System;
using System.IO;

namespace ColorVision.Common.Utilities
{
    public class MemorySize
    {

        //所给路径中所对应的文件大小
        public static long FileSize(string filePath)
        {
            //定义一个FileInfo对象，是指与filePath所指向的文件相关联，以获取其大小
            FileInfo fileInfo = new FileInfo(filePath);
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
                DirectoryInfo di = new DirectoryInfo(dirPath);

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
                DirectoryInfo di = new DirectoryInfo(dirPath);

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

