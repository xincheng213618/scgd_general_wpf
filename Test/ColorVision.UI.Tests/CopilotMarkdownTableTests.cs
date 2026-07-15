#pragma warning disable CA1707
using ColorVision.Copilot;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotMarkdownTableTests
{
    [Fact]
    public void MarkdownView_RendersDatabaseSummaryAsNativeTableBlock()
    {
        RunOnStaThread(() =>
        {
            var view = new CopilotMarkdownView
            {
                Markdown = "### 📊 数据库总览\n\n| 分类 | 表名 | 行数 |\n|------|------|:----:|\n| **测量结果（Results）** | | |\n| | `t_scgd_measure_batch` | **0** |\n\n---\n\n结论",
            };
            var window = new Window
            {
                Content = view,
                Height = 420,
                ShowInTaskbar = false,
                Width = 520,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(250));

                var viewer = Assert.IsType<RichTextBox>(LogicalTreeHelper.FindLogicalNode(view, "DocumentViewer"));
                var table = Assert.Single(viewer.Document.Blocks.OfType<Table>());
                Assert.Equal(3, table.Columns.Count);
                Assert.Equal(3, table.RowGroups.Single().Rows.Count);
                Assert.Equal(GridUnitType.Pixel, table.Columns[0].Width.GridUnitType);
                Assert.True(table.Columns[0].Width.Value >= 96);
                Assert.Equal(GridUnitType.Star, table.Columns[1].Width.GridUnitType);
                Assert.Equal(GridUnitType.Pixel, table.Columns[2].Width.GridUnitType);
                Assert.True(table.Columns[2].Width.Value >= 72);
                var documentText = new TextRange(viewer.Document.ContentStart, viewer.Document.ContentEnd).Text;
                Assert.Contains("t_scgd_measure_batch", documentText, StringComparison.Ordinal);
                Assert.DoesNotContain("|------|", documentText, StringComparison.Ordinal);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MarkdownView_GivesTwoColumnSummaryANonCollapsingFirstColumn()
    {
        RunOnStaThread(() =>
        {
            var view = new CopilotMarkdownView
            {
                Markdown = "| 文章数 | 最近更新 |\n| ---: | --- |\n| 387 篇 | 2024.11.1 记 |\n| 277 篇 | 关于锚点与未来自我的一次对话 |",
            };
            var window = new Window
            {
                Content = view,
                Height = 260,
                ShowInTaskbar = false,
                Width = 520,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(250));

                var viewer = Assert.IsType<RichTextBox>(LogicalTreeHelper.FindLogicalNode(view, "DocumentViewer"));
                var table = Assert.Single(viewer.Document.Blocks.OfType<Table>());
                Assert.Equal(GridUnitType.Pixel, table.Columns[0].Width.GridUnitType);
                Assert.True(table.Columns[0].Width.Value >= 96);
                Assert.Equal(GridUnitType.Star, table.Columns[1].Width.GridUnitType);
                Assert.Equal(1, CountRenderedLines(Assert.Single(table.RowGroups[0].Rows[0].Cells[0].Blocks.OfType<Paragraph>())));
                Assert.Equal(1, CountRenderedLines(Assert.Single(table.RowGroups[0].Rows[1].Cells[0].Blocks.OfType<Paragraph>())));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MarkdownView_DoesNotTreatOrdinaryPipeCommandAsTable()
    {
        RunOnStaThread(() =>
        {
            var view = new CopilotMarkdownView
            {
                Markdown = "PowerShell: Get-Process | Select-Object Name\nordinary text",
            };
            var window = new Window
            {
                Content = view,
                Height = 240,
                ShowInTaskbar = false,
                Width = 520,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(250));

                var viewer = Assert.IsType<RichTextBox>(LogicalTreeHelper.FindLogicalNode(view, "DocumentViewer"));
                Assert.Empty(viewer.Document.Blocks.OfType<Table>());
                var documentText = new TextRange(viewer.Document.ContentStart, viewer.Document.ContentEnd).Text;
                Assert.Contains("Get-Process | Select-Object Name", documentText, StringComparison.Ordinal);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static int CountRenderedLines(Paragraph paragraph)
    {
        var lineCount = 1;
        var position = paragraph.ContentStart;
        while (position.GetLineStartPosition(1, out var moved) is { } next
            && moved > 0
            && next.CompareTo(paragraph.ContentEnd) < 0)
        {
            lineCount += moved;
            position = next;
        }
        return lineCount;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration,
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
