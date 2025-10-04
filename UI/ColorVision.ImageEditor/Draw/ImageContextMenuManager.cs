using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ͼ�������Ĳ˵�������
    /// </summary>
    public class ImageContextMenuManager
    {
        private readonly ImageViewModel _viewModel;
        private readonly DrawCanvas _image;
        private readonly ContextMenu _contextMenu;
        private readonly List<IDVContextMenu> _contextMenuProviders;

        public ImageContextMenuManager(ImageViewModel viewModel, DrawCanvas image, ContextMenu contextMenu, List<IDVContextMenu> contextMenuProviders)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _image = image ?? throw new ArgumentNullException(nameof(image));
            _contextMenu = contextMenu ?? throw new ArgumentNullException(nameof(contextMenu));
            _contextMenuProviders = contextMenuProviders ?? throw new ArgumentNullException(nameof(contextMenuProviders));
        }

        /// <summary>
        /// ���������Ĳ˵����¼�
        /// </summary>
        public void HandleContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            _contextMenu.Items.Clear();

            if (_viewModel.ImageEditMode)
            {
                Point MouseDownP = Mouse.GetPosition(_image);
                var MouseVisual = _image.GetVisual<Visual>(MouseDownP);
                Type type = MouseVisual.GetType();

                if (MouseVisual is SelectEditorVisual selectEditorVisual && selectEditorVisual.GetVisual(MouseDownP) is ISelectVisual selectVisual)
                {
                    foreach (var provider in _contextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(_viewModel, selectVisual);
                            foreach (var item in items)
                                _contextMenu.Items.Add(item);
                        }
                    }
                    foreach (var provider in _contextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectEditorVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(_viewModel, selectEditorVisual);
                            foreach (var item in items)
                                _contextMenu.Items.Add(item);
                        }
                    }
                }
                else
                {
                    foreach (var provider in _contextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(type))
                        {
                            var items = provider.GetContextMenuItems(_viewModel, MouseVisual);
                            foreach (var item in items)
                                _contextMenu.Items.Add(item);
                        }
                    }
                }

                if (_contextMenu.Items.Count == 0)
                    CreateStandardContextMenu();
            }
            else
            {
                CreateStandardContextMenu();
            }
        }

        /// <summary>
        /// ������׼�����Ĳ˵�
        /// </summary>
        public void CreateStandardContextMenu()
        {
            List<MenuItemMetadata> MenuItemMetadatas = new List<MenuItemMetadata>();
            if (_viewModel.IImageOpen != null)
                MenuItemMetadatas.AddRange(_viewModel.IImageOpen.GetContextMenuItems(_viewModel.Config));

            foreach (var item in AssemblyService.Instance.LoadImplementations<IImageContentMenuProvider>())
            {
                MenuItemMetadatas.AddRange(item.GetContextMenuItems(_viewModel.Config));
            }



            // ��ӱ�׼�˵���
            AddStandardMenuItems(MenuItemMetadatas);

            var iMenuItems = MenuItemMetadatas.OrderBy(item => item.Order).ToList();
            
            // �ݹ鴴���˵��ṹ
            void CreateMenu(MenuItem parentMenuItem, string OwnerGuid)
            {
                var iMenuItems1 = iMenuItems.FindAll(a => a.OwnerGuid == OwnerGuid).OrderBy(a => a.Order).ToList();
                for (int i = 0; i < iMenuItems1.Count; i++)
                {
                    var iMenuItem = iMenuItems1[i];
                    string GuidId = iMenuItem.GuidId ?? Guid.NewGuid().ToString();
                    MenuItem menuItem = new()
                    {
                        Header = iMenuItem.Header,
                        Icon = iMenuItem.Icon,
                        InputGestureText = iMenuItem.InputGestureText,
                        Command = iMenuItem.Command,
                        Tag = iMenuItem,
                        Visibility = iMenuItem.Visibility,
                    };

                    CreateMenu(menuItem, GuidId);
                    if (i > 0 && iMenuItem.Order - iMenuItems1[i - 1].Order > 4 && iMenuItem.Visibility == Visibility.Visible)
                    {
                        parentMenuItem.Items.Add(new Separator());
                    }
                    parentMenuItem.Items.Add(menuItem);
                }
                foreach (var item in iMenuItems1)
                {
                    iMenuItems.Remove(item);
                }
            }

            // ���������˵�
            var iMenuItemMetas = MenuItemMetadatas
                .Where(item => item.OwnerGuid == MenuItemConstants.Menu && item.Visibility == Visibility.Visible)
                .OrderBy(item => item.Order)
                .ToList();

            for (int i = 0; i < iMenuItemMetas.Count; i++)
            {
                MenuItemMetadata menuItemMeta = iMenuItemMetas[i];
                MenuItem menuItem = new()
                {
                    Header = menuItemMeta.Header,
                    Command = menuItemMeta.Command,
                    Icon = menuItemMeta.Icon,
                    InputGestureText = menuItemMeta.InputGestureText,
                };
                if (menuItemMeta.GuidId != null)
                    CreateMenu(menuItem, menuItemMeta.GuidId);
                if (i > 0 && menuItemMeta.Order - iMenuItemMetas[i - 1].Order > 4)
                    _contextMenu.Items.Add(new Separator());

                _contextMenu.Items.Add(menuItem);
            }

            // ���λͼ����ģʽ�˵�
            AddBitmapScalingModeMenu();
        }

        /// <summary>
        /// ���λͼ����ģʽ�˵�
        /// </summary>
        private void AddBitmapScalingModeMenu()
        {
            MenuItem menuItemBitmapScalingMode = new() { Header = ColorVision.ImageEditor.Properties.Resources.BitmapScalingMode };
            
            void UpdateBitmapScalingMode()
            {
                var ime = RenderOptions.GetBitmapScalingMode(_image);
                menuItemBitmapScalingMode.Items.Clear();
                foreach (var item in Enum.GetValues(typeof(BitmapScalingMode)).Cast<BitmapScalingMode>().GroupBy(mode => (int)mode).Select(group => group.First()))
                {
                    MenuItem menuItem1 = new() { Header = item.ToString() };
                    if (ime != item)
                    {
                        menuItem1.Click += (s, e) =>
                        {
                            RenderOptions.SetBitmapScalingMode(_image, item);
                        };
                    }
                    menuItem1.IsChecked = ime == item;
                    menuItemBitmapScalingMode.Items.Add(menuItem1);
                }
            }
            
            menuItemBitmapScalingMode.SubmenuOpened += (s, e) => UpdateBitmapScalingMode();
            UpdateBitmapScalingMode();
            _contextMenu.Items.Insert(4, menuItemBitmapScalingMode);
        }

        /// <summary>
        /// ��ӱ�׼�˵���
        /// </summary>
        /// <param name="MenuItemMetadatas">�˵���Ԫ�����б�</param>
        private void AddStandardMenuItems(List<MenuItemMetadata> MenuItemMetadatas)
        {
            // ��/���ͼ��
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenImage", Order = 10, Header = Properties.Resources.Open, Command = _viewModel.OpenImageCommand, Icon = MenuItemIcon.TryFindResource("DIOpen") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "ClearImage", Order = 11, Header = Properties.Resources.Clear, Command = _viewModel.ClearImageCommand, Icon = MenuItemIcon.TryFindResource("DIDelete") });

            // ���Ų˵�
            foreach (var item in _viewModel.IEditorToolFactory.IIEditorToolContextMenus)
            {
                MenuItemMetadatas.AddRange(item.GetContextMenuItems());
            }


            // ��������
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Full", Order = 200, Header = Properties.Resources.FullScreen, Command = _viewModel.FullCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "SaveAsImage", Order = 300, Header = Properties.Resources.SaveAsImage, Command = _viewModel.SaveAsImageCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Print", Order = 300, Header = Properties.Resources.Print, Command = _viewModel.PrintImageCommand, Icon = MenuItemIcon.TryFindResource("DIPrint"), InputGestureText = "Ctrl+P" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Property", Order = 9999, Command = _viewModel.PropertyCommand, Header = Properties.Resources.Property, Icon = MenuItemIcon.TryFindResource("DIProperty"), InputGestureText = "Tab" });
        }
    }
}
