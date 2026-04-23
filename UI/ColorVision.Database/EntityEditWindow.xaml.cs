using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Windows;

namespace ColorVision.Database
{
    public partial class EntityEditWindow : Window
    {
        private readonly object _entity;

        public EntityEditWindow(object entity, string title)
        {
            _entity = entity;
            InitializeComponent();
            Title = title;
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            var editor = PropertyEditorHelper.GenPropertyEditorControl(_entity);
            ContentGrid.Children.Add(editor);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
