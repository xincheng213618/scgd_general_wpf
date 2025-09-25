#pragma warning disable CA1711,CA2211
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DrawingVisualBase : DrawingVisual, INotifyPropertyChanged, ISelectVisual
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public virtual BaseProperties BaseAttribute { get; }
        public virtual int ID { get; set; }
        public virtual void Render() { }

        public virtual Rect GetRect() => new Rect();

        public virtual void SetRect(Rect rect)
        {

        }

    }

    public class DrawingVisualBase<T>: DrawingVisualBase where T : BaseProperties, new()
    {
        public override BaseProperties BaseAttribute => Attribute;

        public override int ID { get => Attribute.Id; set => Attribute.Id = value; }

        public object Tag { get; set; }

        public object ToolTip { get; set; }

        public T Attribute { get; set; }
    }

    public class DrawingVisualBaseDVContextMenu : IDVContextMenu
    {
        public Type ContextType => typeof(DrawingVisualBase);

        public IEnumerable<MenuItem> GetContextMenuItems(ImageViewModel imageViewModel, object obj)
        {
            List<MenuItem> MenuItems = new List<MenuItem>();
            if (obj is DrawingVisualBase visual)
            {
                MenuItem menuIte2 = new() { Header = "删除" };
                menuIte2.Click += (s, e) =>
                {
                    imageViewModel.Image.RemoveVisualCommand(visual);
                    imageViewModel.SelectEditorVisual.ClearRender();
                };
                MenuItems.Add(menuIte2);

                MenuItem menuIte3 = new() { Header = "Top" };
                menuIte3.Click += (s, e) =>
                {
                    imageViewModel.Image.TopVisual(visual);
                };
                MenuItems.Add(menuIte3);

                MenuItem menuItem4 = new() { Header = "编辑" };
                menuItem4.Click += (s, e) =>
                {
                    new PropertyEditorWindow(visual.BaseAttribute) { Owner =Application.Current.GetActiveWindow(),WindowStartupLocation =WindowStartupLocation.CenterOwner}.ShowDialog();
                };
                MenuItems.Add(menuItem4);
            }
            return MenuItems;
        }
    }
}
