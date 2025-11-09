using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace ColorVision.Engine.Reports
{

    public static class ReportGenerator
    {
        public static void GenerateCalibrationReport(string filePath)
        {
            // 创建 PDF 文档
            using var document = new Document(new PdfDocument(new PdfWriter(filePath)));


            // 添加抬头
            Paragraph title = new Paragraph("Spetrum Calibration Data Report")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(20);
            document.Add(title);

            // 添加空行
            document.Add(new Paragraph("\n"));

            // 创建表格
            Table table = new Table(3, true);
            table.AddCell("Company Name");
            table.AddCell("Calibration1");
            table.AddCell("Calibration2");

            // 添加数据
            table.AddCell("Company A");
            table.AddCell("1.23");
            table.AddCell("4.56");

            table.AddCell("Company B");
            table.AddCell("7.89");
            table.AddCell("0.12");

            // 添加表格到文档
            document.Add(table);

            // 关闭文档
            document.Close();

            // 打开 PDF
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

        }
    }
}
