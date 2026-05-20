using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using ProjectARVRPro.Process;
using System.IO;

namespace ProjectARVRPro.Exports
{
    public sealed class JinxingInspectionXlsxExporter : ITestResultCustomExporter
    {
        private const string ResultSheetName = "雷鸟光机测试结果表";
        private const string SpecSheetName = "Sheet1";

        private static readonly string[] TemplateFileNames =
        [
            "金星1.0光机抽检规格（视彩成像色度计）(1).xlsx",
            "金星1.0光机抽检规格（视彩成像色度计）.xlsx",
            "Jinxing10Inspection.xlsx",
        ];

        public CustomTestResultOutputProfile Profile => CustomTestResultOutputProfile.金星1_0光机抽检规格_视彩成像色度计;

        public string FileSuffix => "JinxingInspection";

        public string Export(ObjectiveTestResultExportContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Directory.CreateDirectory(context.OutputDirectory);

            using IWorkbook workbook = CreateWorkbook(context.TemplateDirectory);
            ISheet sheet = workbook.GetSheet(ResultSheetName)
                ?? (workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : workbook.CreateSheet(ResultSheetName));
            int dataRowIndex = GetNextDataRowIndex(sheet);
            IRow row = sheet.GetRow(dataRowIndex) ?? sheet.CreateRow(dataRowIndex);

            var styles = ExportStyles.Create(workbook);
            WriteDataRow(row, dataRowIndex - 1, context, styles);

            string outputPath = Path.Combine(context.OutputDirectory, $"{context.BaseFileName}_{FileSuffix}.xlsx");
            using FileStream output = File.Create(outputPath);
            workbook.Write(output);
            return outputPath;
        }

        private static IWorkbook CreateWorkbook(string? templateDirectory)
        {
            string? templatePath = ResolveTemplatePath(templateDirectory);
            if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
            {
                using FileStream input = File.OpenRead(templatePath);
                return new XSSFWorkbook(input);
            }

            var workbook = new XSSFWorkbook();
            CreateBuiltInResultSheet(workbook);
            CreateBuiltInSpecSheet(workbook);
            return workbook;
        }

        private static string? ResolveTemplatePath(string? templateDirectory)
        {
            if (string.IsNullOrWhiteSpace(templateDirectory))
                return null;

            if (File.Exists(templateDirectory))
                return templateDirectory;

            if (!Directory.Exists(templateDirectory))
                return null;

            foreach (string fileName in TemplateFileNames)
            {
                string candidate = Path.Combine(templateDirectory, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static void CreateBuiltInResultSheet(IWorkbook workbook)
        {
            ISheet sheet = workbook.CreateSheet(ResultSheetName);
            var styles = ExportStyles.Create(workbook);
            IRow header = sheet.CreateRow(0);
            IRow subHeader = sheet.CreateRow(1);
            header.HeightInPoints = 36;
            subHeader.HeightInPoints = 24;

            for (int column = 0; column < 40; column++)
            {
                (header.GetCell(column) ?? header.CreateCell(column)).CellStyle = styles.HeaderStyle;
                (subHeader.GetCell(column) ?? subHeader.CreateCell(column)).CellStyle = styles.HeaderStyle;
            }

            SetString(header, 0, "序号", styles.HeaderStyle);
            SetString(header, 1, "SN", styles.HeaderStyle);
            SetString(header, 2, "HFOV（）", styles.HeaderStyle);
            SetString(header, 3, "VFOV（）", styles.HeaderStyle);
            SetString(header, 4, "DFOV（23.5±0.5°）", styles.HeaderStyle);
            SetString(header, 5, "MTF@2001p/mm（≥0.15）（V）", styles.HeaderStyle);
            SetString(header, 10, "MTF@2001p/mm（≥0.15）(H)", styles.HeaderStyle);
            SetString(header, 15, "MTF@1001p/mm（≥0.35）(V)", styles.HeaderStyle);
            SetString(header, 20, "MTF@1001p/mm（≥0.35）(H)", styles.HeaderStyle);
            SetString(header, 25, "MTF@501p/mm（≥0.65）(V)", styles.HeaderStyle);
            SetString(header, 30, "MTF@501p/mm（≥0.65）(H)", styles.HeaderStyle);
            SetString(header, 35, "SMIA TV畸变（< 0.5%）", styles.HeaderStyle);
            SetString(header, 37, "中心亮度（cd/m^2）", styles.HeaderStyle);
            SetString(header, 38, "亮度均匀性（≥65%）", styles.HeaderStyle);
            SetString(header, 39, "颜色均匀性", styles.HeaderStyle);

            WritePointHeaders(subHeader, 5, styles.HeaderStyle);
            WritePointHeaders(subHeader, 10, styles.HeaderStyle);
            WritePointHeaders(subHeader, 15, styles.HeaderStyle);
            WritePointHeaders(subHeader, 20, styles.HeaderStyle);
            WritePointHeaders(subHeader, 25, styles.HeaderStyle);
            WritePointHeaders(subHeader, 30, styles.HeaderStyle);
            SetString(subHeader, 35, "H", styles.HeaderStyle);
            SetString(subHeader, 36, "V", styles.HeaderStyle);

            foreach (CellRangeAddress region in new[]
            {
                new CellRangeAddress(0, 1, 0, 0),
                new CellRangeAddress(0, 1, 1, 1),
                new CellRangeAddress(0, 1, 2, 2),
                new CellRangeAddress(0, 1, 3, 3),
                new CellRangeAddress(0, 1, 4, 4),
                new CellRangeAddress(0, 0, 5, 9),
                new CellRangeAddress(0, 0, 10, 14),
                new CellRangeAddress(0, 0, 15, 19),
                new CellRangeAddress(0, 0, 20, 24),
                new CellRangeAddress(0, 0, 25, 29),
                new CellRangeAddress(0, 0, 30, 34),
                new CellRangeAddress(0, 0, 35, 36),
                new CellRangeAddress(0, 1, 37, 37),
                new CellRangeAddress(0, 1, 38, 38),
                new CellRangeAddress(0, 1, 39, 39),
            })
            {
                sheet.AddMergedRegion(region);
            }

            int[] widths =
            [
                8, 22, 14, 14, 18,
                12, 12, 12, 12, 12,
                12, 12, 12, 12, 12,
                12, 12, 12, 12, 12,
                12, 12, 12, 12, 12,
                12, 12, 12, 12, 12,
                12, 12, 12, 12, 12,
                12, 12, 18, 18, 14
            ];

            for (int i = 0; i < widths.Length; i++)
                sheet.SetColumnWidth(i, widths[i] * 256);

            sheet.CreateFreezePane(2, 2);
        }

        private static void CreateBuiltInSpecSheet(IWorkbook workbook)
        {
            ISheet sheet = workbook.CreateSheet(SpecSheetName);
            var styles = ExportStyles.Create(workbook);
            string[,] values =
            {
                { "规格", "" },
                { "DFOV", "23.5±5°" },
                { "MTF@2001p/mm(V)", "≥0.15" },
                { "MTF@2001p/mm(H)", "≥0.15" },
                { "MTF@1001p/mm(V)", "≥0.35" },
                { "MTF@1001p/mm(H)", "≥0.35" },
                { "MTF@501p/mm(V)", "≥0.65" },
                { "MTF@501p/mm(H)", "≥0.65" },
                { "SMIA TV畸变", "＜0.5%" },
                { "亮度均匀性", "≥65%" },
            };

            for (int i = 0; i < values.GetLength(0); i++)
            {
                IRow row = sheet.GetRow(i + 2) ?? sheet.CreateRow(i + 2);
                SetString(row, 1, values[i, 0], i == 0 ? styles.HeaderStyle : styles.DataStyle);
                SetString(row, 2, values[i, 1], i == 0 ? styles.HeaderStyle : styles.DataStyle);
            }

            sheet.SetColumnWidth(1, 24 * 256);
            sheet.SetColumnWidth(2, 16 * 256);
        }

        private static void WriteDataRow(IRow row, int sequence, ObjectiveTestResultExportContext context, ExportStyles styles)
        {
            var resolver = new ObjectiveTestResultValueResolver(context.Result);
            row.HeightInPoints = 22;

            SetNumber(row, 0, sequence, styles.DataStyle);
            SetString(row, 1, context.SerialNumber, styles.DataStyle);
            SetNumber(row, 2, resolver.Find("W51", "HorizontalFieldOfViewAngle")?.Value, styles.DataStyle);
            SetNumber(row, 3, resolver.Find("W51", "VerticalFieldOfViewAngle")?.Value, styles.DataStyle);
            SetNumber(row, 4, resolver.Find("W51", "DiagonalFieldOfViewAngle")?.Value, styles.DataStyle);

            WriteMtfBlock(row, 5, resolver, "MTFV1", "V", "1", styles.DataStyle);
            WriteMtfBlock(row, 10, resolver, "MTFH1", "H", "1", styles.DataStyle);
            WriteMtfBlock(row, 15, resolver, "MTFV2", "V", "2", styles.DataStyle);
            WriteMtfBlock(row, 20, resolver, "MTFH2", "H", "2", styles.DataStyle);
            WriteMtfBlock(row, 25, resolver, "MTFV4", "V", "4", styles.DataStyle);
            WriteMtfBlock(row, 30, resolver, "MTFH4", "H", "4", styles.DataStyle);

            SetNumber(row, 35, resolver.Find("Distortion", "HorizontalTVDistortion")?.Value, styles.DataStyle);
            SetNumber(row, 36, resolver.Find("Distortion", "VerticalTVDistortion")?.Value, styles.DataStyle);
            SetNumber(row, 37, resolver.Find("W255", "CenterLunimance")?.Value, styles.DataStyle);
            SetNumber(row, 38, resolver.Find("W255", "LuminanceUniformity")?.Value, styles.PercentStyle);
            SetNumber(row, 39, resolver.Find("W255", "ColorUniformity")?.Value, styles.DataStyle);
        }

        private static void WriteMtfBlock(IRow row, int startColumn, ObjectiveTestResultValueResolver resolver, string screen, string axis, string frequency, ICellStyle style)
        {
            for (int point = 1; point <= 5; point++)
            {
                ObjectiveTestItem? item = resolver.FindAny(
                    screen,
                    $"P_{point}_{axis}_{frequency}",
                    $"P_{point}_F_{frequency}");

                SetNumber(row, startColumn + point - 1, item?.Value, style);
            }
        }

        private static void WritePointHeaders(IRow row, int startColumn, ICellStyle style)
        {
            for (int i = 0; i < 5; i++)
                SetString(row, startColumn + i, $"P{i + 1}", style);
        }

        private static int GetNextDataRowIndex(ISheet sheet)
        {
            for (int rowIndex = Math.Max(2, sheet.LastRowNum); rowIndex >= 2; rowIndex--)
            {
                if (HasAnyValue(sheet.GetRow(rowIndex)))
                    return rowIndex + 1;
            }

            return 2;
        }

        private static bool HasAnyValue(IRow? row)
        {
            if (row == null || row.LastCellNum <= 0)
                return false;

            for (int column = Math.Max(0, (int)row.FirstCellNum); column < row.LastCellNum; column++)
            {
                ICell? cell = row.GetCell(column);
                if (cell != null && cell.CellType != CellType.Blank && !string.IsNullOrWhiteSpace(cell.ToString()))
                    return true;
            }

            return false;
        }

        private static void SetString(IRow row, int column, string? value, ICellStyle style)
        {
            ICell cell = row.GetCell(column) ?? row.CreateCell(column);
            cell.CellStyle = style;
            cell.SetCellValue(value ?? string.Empty);
        }

        private static void SetNumber(IRow row, int column, double? value, ICellStyle style)
        {
            ICell cell = row.GetCell(column) ?? row.CreateCell(column);
            cell.CellStyle = style;

            if (value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value))
                cell.SetCellValue(value.Value);
            else
                cell.SetCellValue(string.Empty);
        }

        private sealed class ExportStyles
        {
            public required ICellStyle HeaderStyle { get; init; }

            public required ICellStyle DataStyle { get; init; }

            public required ICellStyle PercentStyle { get; init; }

            public static ExportStyles Create(IWorkbook workbook)
            {
                IFont headerFont = workbook.CreateFont();
                headerFont.IsBold = true;

                ICellStyle header = workbook.CreateCellStyle();
                header.SetFont(headerFont);
                header.Alignment = HorizontalAlignment.Center;
                header.VerticalAlignment = VerticalAlignment.Center;
                header.WrapText = true;
                ApplyBorder(header);

                ICellStyle data = workbook.CreateCellStyle();
                data.Alignment = HorizontalAlignment.Center;
                data.VerticalAlignment = VerticalAlignment.Center;
                ApplyBorder(data);

                ICellStyle percent = workbook.CreateCellStyle();
                percent.CloneStyleFrom(data);
                percent.DataFormat = workbook.CreateDataFormat().GetFormat("0.00%");

                return new ExportStyles
                {
                    HeaderStyle = header,
                    DataStyle = data,
                    PercentStyle = percent,
                };
            }

            private static void ApplyBorder(ICellStyle style)
            {
                style.BorderTop = BorderStyle.Thin;
                style.BorderBottom = BorderStyle.Thin;
                style.BorderLeft = BorderStyle.Thin;
                style.BorderRight = BorderStyle.Thin;
            }
        }
    }
}
