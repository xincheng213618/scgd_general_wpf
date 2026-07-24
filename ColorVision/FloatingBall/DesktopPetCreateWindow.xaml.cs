using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.FloatingBall
{
    public partial class DesktopPetCreateWindow : Window
    {
        private bool _isCreating;

        public DesktopPetCreateWindow()
        {
            InitializeComponent();
        }

        public string? CreatedAssetId { get; private set; }

        public string? CreatedDisplayName { get; private set; }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择桌面宠物精灵表",
                Filter = "宠物精灵表 (*.webp;*.png)|*.webp;*.png|WebP 图片 (*.webp)|*.webp|PNG 图片 (*.png)|*.png",
                CheckFileExists = true,
                Multiselect = false,
            };

            if (dialog.ShowDialog(this) != true)
                return;

            SpriteSheetPathTextBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(PetNameTextBox.Text))
                PetNameTextBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCreating)
                return;

            var version = SpriteVersionComboBox.SelectedItem is ComboBoxItem { Tag: string versionText }
                && int.TryParse(versionText, out var selectedVersion)
                    ? selectedVersion
                    : 2;

            _isCreating = true;
            CreateButton.IsEnabled = false;
            StatusText.Foreground = System.Windows.Media.Brushes.DimGray;
            StatusText.Text = "正在校验并导入素材…";
            try
            {
                var displayName = PetNameTextBox.Text.Trim();
                var result = await DesktopPetPackageService.ImportAsync(new DesktopPetImportRequest(
                    displayName,
                    DescriptionTextBox.Text,
                    SpriteSheetPathTextBox.Text,
                    version));

                CreatedAssetId = result.AssetId;
                CreatedDisplayName = displayName;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                StatusText.Foreground = System.Windows.Media.Brushes.Firebrick;
                StatusText.Text = ex.Message;
            }
            finally
            {
                _isCreating = false;
                CreateButton.IsEnabled = true;
            }
        }
    }
}
