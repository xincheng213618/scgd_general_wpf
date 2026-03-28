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
    /// </summary>
    public class DockViewManager : IViewManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DockViewManager));

        private readonly LayoutDocumentPane _documentPane;
        private readonly DockLayoutManager _layoutManager;

        /// <summary>
        /// 已注册的视图控件列表（与 ViewGridManager.Views 对应）
        /// </summary>
        public List<Control> Views { get; } = new();

        /// <summary>
        /// 控件 → LayoutDocument 的映射
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
        /// 创建 DockViewManager。
        /// </summary>
        /// <param name="documentPane">主文档窗格，视图将作为 LayoutDocument 添加到此处</param>
        /// <param name="layoutManager">布局管理器，用于注册文档以支持布局持久化</param>
        public DockViewManager(LayoutDocumentPane documentPane, DockLayoutManager layoutManager)
        {
            _documentPane = documentPane;
            _layoutManager = layoutManager;
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
            CreateDocumentForView(control);
            ViewMax = Views.Count;
            return Views.IndexOf(control);
        }

        public int AddView(int index, Control control)
        {
            if (control == null) return -1;
            if (Views.Contains(control)) return Views.IndexOf(control);

            Views.Insert(Math.Clamp(index, 0, Views.Count), control);
            CreateDocumentForView(control, insertFirst: index == 0);
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
            if (!_viewDocuments.TryGetValue(control, out var doc)) return;

            if (viewIndex >= 0)
            {
                // 显示并激活文档
                ShowDocument(doc);
            }
            else if (viewIndex == -1)
            {
                // 隐藏文档
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
            // Dock 模式下，每个位置都是独立的，概念上始终"空"
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
            // Dock 模式：显示前 nums 个视图的文档标签，隐藏其余
            ViewMax = nums;
            for (int i = 0; i < Views.Count; i++)
            {
                if (_viewDocuments.TryGetValue(Views[i], out var doc))
                {
                    if (i < nums)
                    {
                        ShowDocument(doc);
                        if (Views[i] is IView view && view.View.ViewIndex < 0)
                            view.View.ViewIndex = i;
                    }
                    else
                    {
                        HideDocument(doc);
                    }
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
            // 隐藏所有，只显示指定控件
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
            ViewMax = 1;
        }

        public void SetViewNum(int num)
        {
            if (num == -1)
            {
                // 显示所有
                for (int i = 0; i < Views.Count; i++)
                {
                    if (_viewDocuments.TryGetValue(Views[i], out var doc))
                    {
                        ShowDocument(doc);
                        if (Views[i] is IView view)
                            view.View.ViewIndex = i;
                    }
                }
                ViewMax = Views.Count;
                return;
            }

            if (Views.Count == 0) return;

            int count = Math.Min(num, Views.Count);
            for (int i = 0; i < count; i++)
            {
                if (_viewDocuments.TryGetValue(Views[i], out var doc))
                    ShowDocument(doc);
            }
            ViewMax = count;
        }

        public void SetSingleWindowView(Control control)
        {
            if (!_viewDocuments.TryGetValue(control, out var doc)) return;
            if (control is not IView view) return;

            // 先从文档窗格中移除
            HideDocument(doc);
            Views.Remove(control);
            _viewDocuments.Remove(control);

            // 创建独立窗口
            var window = new Window { Owner = Application.Current.MainWindow };
            var titleBinding = new Binding("Title") { Source = view.View };
            window.SetBinding(Window.TitleProperty, titleBinding);
            var iconBinding = new Binding("Icon") { Source = view.View };
            window.SetBinding(Window.IconProperty, iconBinding);

            ViewIndexChangedHandler? eventHandler = null;
            eventHandler = (oldIdx, newIdx) =>
            {
                window.Close();
                view.View.ViewIndexChangedEvent -= eventHandler;
            };
            view.View.ViewIndexChangedEvent += eventHandler;

            // 从当前父控件移除（安全起见）
            if (control.Parent is System.Windows.Controls.Panel panel)
                panel.Children.Remove(control);

            var grid = new Grid();
            grid.Children.Add(control);
            window.Content = grid;

            window.Closed += (s, e) =>
            {
                view.View.ViewIndexChangedEvent -= eventHandler;
                // 窗口关闭后重新添加为文档
                if (grid.Children.Contains(control))
                    grid.Children.Remove(control);
                Views.Add(control);
                CreateDocumentForView(control);
                view.View.ViewIndex = IsGridEmpty(view.View.PreViewIndex) ? view.View.PreViewIndex : -1;
            };

            window.Show();
        }

        /// <summary>
        /// 为视图控件创建 LayoutDocument 并添加到文档窗格。
        /// </summary>
        private void CreateDocumentForView(Control control, bool insertFirst = false)
        {
            // 从当前父控件移除（可能来自 ViewGridManager 或其他容器）
            if (control.Parent is System.Windows.Controls.Panel panel)
                panel.Children.Remove(control);

            string title = "View";
            if (control is IView view)
            {
                view.View.ViewGridManager = this;
                title = !string.IsNullOrEmpty(view.View.Title) ? view.View.Title : $"View {Views.IndexOf(control) + 1}";
            }

            string contentId = $"DockView_{control.GetHashCode()}";

            var doc = new LayoutDocument
            {
                Title = title,
                ContentId = contentId,
                Content = control,
                CanClose = true,
                CanFloat = true
            };

            // 标题绑定（如果有 IView）
            if (control is IView viewForBinding)
            {
                var binding = new Binding("Title") { Source = viewForBinding.View };
                BindingOperations.SetBinding(doc, LayoutDocument.TitleProperty, binding);
            }

            // 隐藏状态：初始 ViewIndex == -1 时不显示
            if (control is IView viewCheck && viewCheck.View.ViewIndex < 0)
            {
                // 添加但不激活
            }

            if (insertFirst && _documentPane.ChildrenCount > 0)
                _documentPane.Children.Insert(0, doc);
            else
                _documentPane.Children.Add(doc);

            _viewDocuments[control] = doc;

            // 注册到 DockLayoutManager 以支持持久化
            _layoutManager.RegisterDocument(contentId, control, title, true);

            log.Debug($"DockViewManager: 添加视图文档 '{title}' (ContentId={contentId})");
        }

        /// <summary>
        /// 显示 LayoutDocument（如果已隐藏/关闭，重新添加到窗格）
        /// </summary>
        private void ShowDocument(LayoutDocument doc)
        {
            if (doc.Parent == null)
            {
                // 文档已被关闭，重新添加
                _documentPane.Children.Add(doc);
            }
            doc.IsVisible = true;
        }

        /// <summary>
        /// 隐藏 LayoutDocument
        /// </summary>
        private static void HideDocument(LayoutDocument doc)
        {
            doc.Close();
        }
    }
}
