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
                Assert.Equal(GridUnitType.Pixel, table.Columns[1].Width.GridUnitType);
                Assert.Equal(GridUnitType.Pixel, table.Columns[2].Width.GridUnitType);
                Assert.True(table.Columns[2].Width.Value >= 72);
                Assert.InRange(table.Columns.Sum(column => column.Width.Value), viewer.ActualWidth - 12, viewer.ActualWidth);
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
                Assert.Equal(GridUnitType.Pixel, table.Columns[1].Width.GridUnitType);
                Assert.InRange(table.Columns.Sum(column => column.Width.Value), viewer.ActualWidth - 12, viewer.ActualWidth);
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
    public void MarkdownView_UsesKeyValueRecordsWhenNarrativeTableIsTooNarrow()
    {
        RunOnStaThread(() =>
        {
            var view = new CopilotMarkdownView
            {
                Markdown = "| 项目 | 内容 |\n| --- | --- |\n| 建站工具 | 这是一个在狭窄聊天面板里会形成很多碎片行的较长说明，因此应当改用纵向键值布局。 |",
            };
            var window = new Window
            {
                Content = view,
                Height = 320,
                ShowInTaskbar = false,
                Width = 280,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(250));

                var viewer = Assert.IsType<RichTextBox>(LogicalTreeHelper.FindLogicalNode(view, "DocumentViewer"));
                Assert.Empty(viewer.Document.Blocks.OfType<Table>());
                var documentText = new TextRange(viewer.Document.ContentStart, viewer.Document.ContentEnd).Text;
                Assert.Contains("建站工具", documentText, StringComparison.Ordinal);
                Assert.Contains("纵向键值布局", documentText, StringComparison.Ordinal);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MarkdownView_KeepsTwoColumnKeyValueTableInsideVisibleWidth()
    {
        RunOnStaThread(() =>
        {
            var view = new CopilotMarkdownView
            {
                Markdown = "| 项目 | 内容 |\n|------|------|\n| **网站名称** | 信成的博客 |\n| **副标题** | xincheng |\n| **作者** | Mr.Xin |\n| **类型** | 个人博客 |\n| **建站工具** | [Hexo](https://hexo.io/)（静态博客生成器） |\n| **文章总数** | 30 篇 |\n| **RSS 订阅** | 有（atom.xml） |\n| **最后更新** | 2022-10-03 |",
            };
            var window = new Window
            {
                Content = view,
                Height = 900,
                ShowInTaskbar = false,
                Width = 820,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(250));

                var viewer = Assert.IsType<RichTextBox>(LogicalTreeHelper.FindLogicalNode(view, "DocumentViewer"));
                var table = Assert.Single(viewer.Document.Blocks.OfType<Table>());
                var firstRow = table.RowGroups.Single().Rows[1];
                var labelRect = firstRow.Cells[0].ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var valueRect = firstRow.Cells[1].ContentStart.GetCharacterRect(LogicalDirection.Forward);

                Assert.True(labelRect.X >= 0 && valueRect.X > labelRect.X);
                Assert.InRange(valueRect.X - labelRect.X, 80, 260);
                Assert.True(valueRect.X < viewer.ActualWidth - 80, $"Value column starts outside the visible width at {valueRect.X:0.##}/{viewer.ActualWidth:0.##}.");
                Assert.InRange(firstRow.Cells[1].ContentEnd.GetCharacterRect(LogicalDirection.Backward).Y - valueRect.Y, 0, 60);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MarkdownView_KeepsKeyValueRowsCompactInsideChatMessageHierarchy()
    {
        RunOnStaThread(() =>
        {
            var view = new CopilotMarkdownView
            {
                Markdown = "| 项目 | 内容 |\n|------|------|\n| **网站名称** | 信成的博客 |\n| **副标题** | xincheng |\n| **作者** | Mr.Xin |\n| **类型** | 个人博客 |\n| **建站工具** | [Hexo](https://hexo.io/)（静态博客生成器） |\n| **文章总数** | 30 篇 |\n| **RSS 订阅** | 有（atom.xml） |\n| **最后更新** | 2022-10-03 |",
            };
            var timeline = new ItemsControl();
            timeline.Items.Add(view);
            var assistantPanel = new StackPanel();
            assistantPanel.Children.Add(timeline);
            var messages = new ItemsControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(16, 12, 16, 12),
                MaxWidth = 880,
            };
            messages.Items.Add(assistantPanel);
            var window = new Window
            {
                Content = new ScrollViewer
                {
                    Content = messages,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                },
                Height = 1000,
                ShowInTaskbar = false,
                Width = 2000,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(350));

                var viewer = Assert.IsType<RichTextBox>(LogicalTreeHelper.FindLogicalNode(view, "DocumentViewer"));
                var table = Assert.Single(viewer.Document.Blocks.OfType<Table>());
                var rows = table.RowGroups.Single().Rows.Skip(1).ToArray();
                Assert.Equal(8, rows.Length);
                Assert.True(view.ActualHeight < 360, $"Expected compact key-value rows, actual view height was {view.ActualHeight:0.##}.");
                Assert.InRange(table.Columns.Sum(column => column.Width.Value), viewer.ActualWidth - 12, viewer.ActualWidth);
                Assert.All(rows, row =>
                {
                    var labelRect = row.Cells[0].ContentStart.GetCharacterRect(LogicalDirection.Forward);
                    var valueRect = row.Cells[1].ContentStart.GetCharacterRect(LogicalDirection.Forward);
                    var valueEndRect = row.Cells[1].ContentEnd.GetCharacterRect(LogicalDirection.Backward);
                    Assert.InRange(valueRect.X - labelRect.X, 80, 260);
                    Assert.InRange(valueEndRect.Y - valueRect.Y, 0, 60);
                });
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
