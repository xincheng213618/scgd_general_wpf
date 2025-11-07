using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Batch.IVL
{
    public static class ILvCsvParser
    {
        // 兼容 Camera_IVL_xxx.csv 与 SP_IVL_xxx.csv
        // - Current 列：包含 "Current"（区分大小写与否均可）
        // - Lv 列：包含 "Lv" 且包含 "cd/m2"（原导出为 Lv(cd/m2)）
        public static (List<double> currents, List<double> lvs) ParseCurrentLvFromCsv(string path)
        {
            var currents = new List<double>();
            var lvs = new List<double>();

            using var sr = new StreamReader(path);
            string? header = sr.ReadLine();
            if (header == null)
                throw new InvalidOperationException("CSV 文件为空。");

            string[] headerCols = SplitCsvLine(header);
            int iCurrent = FindColumnIndex(headerCols, contains: "current");
            int iLv = FindColumnIndex(headerCols, contains: "lv"); // 后续再验证单位

            if (iCurrent < 0 || iLv < 0)
                throw new InvalidOperationException("未找到列：Current 与 Lv。请确认表头包含 Current(mA) 与 Lv(cd/m2)");

            // 尝试尽量确认 Lv 是亮度
            // 允许 "Lv(cd/m2)" 或 "Lv"；如果存在多个匹配，优先带 cd/m2 的
            iLv = DisambiguateLvIndex(headerCols, iLv);

            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] cols = SplitCsvLine(line);
                if (iCurrent >= cols.Length || iLv >= cols.Length) continue;

                if (TryParseDouble(cols[iCurrent], out double iVal) &&
                    TryParseDouble(cols[iLv], out double lvVal))
                {
                    currents.Add(iVal);
                    lvs.Add(lvVal);
                }
            }

            return (currents, lvs);
        }

        private static int FindColumnIndex(string[] headerCols, string contains)
        {
            // 不区分大小写查找第一个包含关键字的列
            for (int i = 0; i < headerCols.Length; i++)
            {
                if (headerCols[i].IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }
            return -1;
        }

        private static int DisambiguateLvIndex(string[] headerCols, int currentLvIndex)
        {
            // 如果存在多个 Lv，尽量选择包含 cd/m2 的列名
            var candidates = headerCols
                .Select((name, idx) => (name, idx))
                .Where(t => t.name.IndexOf("lv", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (candidates.Count <= 1) return currentLvIndex;

            var withUnit = candidates
                .Where(t => t.name.IndexOf("cd/m2", StringComparison.OrdinalIgnoreCase) >= 0
                         || t.name.IndexOf("cd/m²", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (withUnit.Count > 0) return withUnit[0].idx;
            return currentLvIndex;
        }

        private static string[] SplitCsvLine(string line)
        {
            // 简单按逗号切分（IVL 导出未使用引号转义），忽略尾部空列
            // 注意：SP 导出每行末尾常有一个多余的逗号
            return line.Split(',');
        }

        private static bool TryParseDouble(string s, out double value)
        {
            // 优先按 InvariantCulture（小数点 .）
            if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
                return true;

            // 回退：当前区域性
            return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value);
        }
    }
}
