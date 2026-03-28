using AvalonDock.Layout;
using ColorVision.UI.Views;
using log4net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// 基于 AvalonDock 的 IViewManager 实现。
    /// 每个 IView 对应一个独立的 LayoutDocument，支持停靠/浮动/标签切换。
    /// 替代 ViewGridManager 的 N 宫格模式，使视图布局完全由 AvalonDock 管理。
    /// 视图文档按需创建（懒加载）：AddView 仅注册，SetViewIndex >= 0 时才创建标签。
    /// </summary>
    public class DockViewManager : IViewManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DockViewManager));

        private readonly LayoutDocumentPane _documentPane;

        /// <summary>
        /// 已注册的视图控件列表
        /// </summary>
        public List<Control> Views { get; } = new();

        /// <summary>
        /// 控件 → LayoutDocument 的映射（仅当文档已创建时存在）
        /// </summary>
        private readonly Dictionary<Control, LayoutDocument> _viewDocuments = new();

        public event ViewMaxChangedHandler? ViewMaxChangedEvent;

        public int ViewMax
        {
            get => _viewMax;
            set
            {
                if (_viewMax == value) return;
                ViewMaxChangedEvent?.Invoke(value);
                _viewMax = value;
            }
        }
        private int _viewMax;

        /// <summary>
        /// 上一次激活的视图控件，用于 DeviceControl 切换时恢复
        /// </summary>
        public Control? LastActiveView { get; private set; }

        /// <summary>
        /// 创建 DockViewManager。
        /// </summary>
        /// <param name="documentPane">主文档窗格，视图将作为 LayoutDocument 添加到此处</param>
        public DockViewManager(LayoutDocumentPane documentPane)
        {
            _documentPane = documentPane;
        }

        public Control? CurrentView
        {
            get
            {
                // 返回当前活动文档的内容
                foreach (var doc in _documentPane.Children.OfType<LayoutDocument>())
                {
                    if (doc.IsActive && doc.Content is Control control && Views.Contains(control))
                        return control;
                }
                // 回退到第一个可见的视图
                return Views.FirstOrDefault(v => _viewDocuments.TryGetValue(v, out var doc) && doc.IsVisible);
            }
        }

        public int AddView(Control control)
        {
            if (control == null) return -1;
            if (Views.Contains(control)) return Views.IndexOf(control);

            Views.Add(control);
            if (control is IView view)
                view.View.ViewGridManager = this;
            ViewMax = Views.Count;
            return Views.IndexOf(control);
        }

        public int AddView(int index, Control control)
        {
            if (control == null) return -1;
            if (Views.Contains(control)) return Views.IndexOf(control);

            Views.Insert(Math.Clamp(index, 0, Views.Count), control);
            if (control is IView view)
                view.View.ViewGridManager = this;
            ViewMax = Views.Count;
            return Views.IndexOf(control);
        }

        public void RemoveView(int index)
        {
            if (index < 0 || index >= Views.Count) return;
            RemoveView(Views[index]);
        }

        public void RemoveView(Control control)
        {
            if (_viewDocuments.TryGetValue(control, out var doc))
            {
                doc.Close();
                _viewDocuments.Remove(control);
            }
            Views.Remove(control);
            ViewMax = Views.Count;
        }

        public void SetViewIndex(Control control, int viewIndex)
        {
            if (!Views.Contains(control)) return;

            if (viewIndex >= 0)
            {
                // 按需创建文档，然后显示并激活
                var doc = EnsureDocument(control);
                ShowDocument(doc);
                LastActiveView = control;
            }
            else if (viewIndex == -1)
            {
                // 隐藏文档（如果存在）
                if (_viewDocuments.TryGetValue(control, out var doc))
                    HideDocument(doc);
            }
            else if (viewIndex == -2)
            {
                // 弹出为独立窗口
                SetSingleWindowView(control);
            }
        }

        public bool IsGridEmpty(int index)
        {
            if (index < 0 || index >= Views.Count) return true;
            var control = Views[index];
            if (_viewDocuments.TryGetValue(control, out var doc))
                return !doc.IsVisible;
            return true;
        }

        public int GetViewNums()
        {
            return _viewDocuments.Values.Count(d => d.IsVisible);
        }

        public void SetViewGrid(int nums)
        {
            ViewMax = nums;
            for (int i = 0; i < Views.Count; i++)
            {
                if (i < nums)
                {
                    var doc = EnsureDocument(Views[i]);
                    ShowDocument(doc);
                    if (Views[i] is IView view && view.View.ViewIndex < 0)
                        view.View.ViewIndex = i;
                }
                else
                {
                    if (_viewDocuments.TryGetValue(Views[i], out var doc))
                        HideDocument(doc);
                }
            }
        }

        public void SetOneView(int main)
        {
            if (main < 0 || main >= Views.Count) return;
            SetOneView(Views[main]);
        }

        public void SetOneView(Control control)
        {
            foreach (var kvp in _viewDocuments)
            {
                if (kvp.Key == control)
                {
                    ShowDocument(kvp.Value);
                    kvp.Value.IsActive = true;
                    if (control is IView view)
                        view.View.ViewIndex = 0;
                }
                else
                {
                    HideDocument(kvp.Value);
                    if (kvp.Key is IView otherView)
                        otherView.View.ViewIndex = -1;
                }
            }
            // 如果控件没有文档，先创建
            if (!_viewDocuments.ContainsKey(control))
            {
                var doc = EnsureDocument(control);
                ShowDocument(doc);
                if (control is IView view)
                    view.View.ViewIndex = 0;
            }
            ViewMax = 1;
            LastActiveView = control;
        }

        public void SetViewNum(int num)
        {
            if (num == -1)
            {
                for (int i = 0; i < Views.Count; i++)
                {
                    var doc = EnsureDocument(Views[i]);
                    ShowDocument(doc);
                    if (Views[i] is IView view)
                        view.View.ViewIndex = i;
                }
                ViewMax = Views.Count;
                return;
            }

            if (Views.Count == 0) return;

            int count = Math.Min(num, Views.Count);
            for (int i = 0; i < count; i++)
            {
                var doc = EnsureDocument(Views[i]);
                ShowDocument(doc);
            }
            ViewMax = count;
        }

        public void SetSingleWindowView(Control control)
        {
            if (control is not IView view) return;

            // 如果有文档，先隐藏
            if (_viewDocuments.TryGetValue(control, out var existingDoc))
            {
                HideDocument(existingDoc);
                _viewDocuments.Remove(control);
            }
            Views.Remove(control);

            // 从当前父控件移除
            DetachFromParent(control);

            // 创建独立窗口
            var window = new Window { Owner = Application.Current.MainWindow };
            window.SetBinding(Window.TitleProperty, new Binding("Title") { Source = view.View });
            window.SetBinding(Window.IconProperty, new Binding("Icon") { Source = view.View });

            ViewIndexChangedHandler? eventHandler = null;
            eventHandler = (oldIdx, newIdx) =>
            {
                window.Close();
                view.View.ViewIndexChangedEvent -= eventHandler;
            };
            view.View.ViewIndexChangedEvent += eventHandler;

            var grid = new Grid();
            grid.Children.Add(control);
            window.Content = grid;

            window.Closed += (s, e) =>
            {
                view.View.ViewIndexChangedEvent -= eventHandler;
                if (grid.Children.Contains(control))
                    grid.Children.Remove(control);
                if (!Views.Contains(control))
                {
                    Views.Add(control);
                    view.View.ViewGridManager = this;
                }
                view.View.ViewIndex = IsGridEmpty(view.View.PreViewIndex) ? view.View.PreViewIndex : -1;
            };

            window.Show();
        }

        /// <summary>
        /// 激活上一次显示的视图文档。
        /// 用于 DeviceControl 面板切换时恢复上次查看的视图。
        /// </summary>
        public void ActivateLastView()
        {
            if (LastActiveView != null && _viewDocuments.TryGetValue(LastActiveView, out var doc))
            {
                ShowDocument(doc);
            }
        }

        /// <summary>
        /// 递增计数器，用于生成唯一的默认视图 ContentId
        /// </summary>
        private int _viewCounter;

        /// <summary>
        /// 确保视图控件有对应的 LayoutDocument。如果没有则创建。
        /// </summary>
        private LayoutDocument EnsureDocument(Control control)
        {
            if (_viewDocuments.TryGetValue(control, out var existing))
                return existing;

            return CreateDocumentForView(control);
        }

        /// <summary>
        /// 为视图控件创建 LayoutDocument 并添加到文档窗格。
        /// </summary>
        private LayoutDocument CreateDocumentForView(Control control)
        {
            DetachFromParent(control);

            _viewCounter++;
            string title = $"View {_viewCounter}";
            if (control is IView view && !string.IsNullOrEmpty(view.View.Title))
                title = view.View.Title;

            string contentId = $"DockView_{_viewCounter}";

            var doc = new LayoutDocument
            {
                Title = title,
                ContentId = contentId,
                Content = control,
                CanClose = true,
                CanFloat = true
            };

            // 标题绑定：标题跟随 View.Title 变化
            if (control is IView viewForBinding)
                BindingOperations.SetBinding(doc, LayoutDocument.TitleProperty, new Binding("Title") { Source = viewForBinding.View });

            _documentPane.Children.Add(doc);
            _viewDocuments[control] = doc;

            log.Debug($"DockViewManager: 创建视图文档 '{title}' (ContentId={contentId})");
            return doc;
        }

        /// <summary>
        /// 显示 LayoutDocument（如果已关闭，重新添加到窗格）
        /// </summary>
        private void ShowDocument(LayoutDocument doc)
        {
            if (doc.Parent == null)
                _documentPane.Children.Add(doc);
            doc.IsActive = true;
        }

        /// <summary>
        /// 隐藏/关闭 LayoutDocument
        /// </summary>
        private static void HideDocument(LayoutDocument doc)
        {
            doc.Close();
        }

        /// <summary>
        /// 从父容器中安全移除控件
        /// </summary>
        private static void DetachFromParent(Control control)
        {
            if (control.Parent is System.Windows.Controls.Panel panel)
                panel.Children.Remove(control);
        }
    }
}
