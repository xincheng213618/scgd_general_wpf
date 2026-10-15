using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.FloatingBall
{
    public partial class DesktopPetCreateWindow : Window
    {
        private DesktopPetCodexAvailability? _codexAvailability;
        private bool _isCreating;

        public DesktopPetCreateWindow()
        {
            InitializeComponent();
            ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
        }

        public string? CreatedAssetId { get; private set; }

        public string? CreatedDisplayName { get; private set; }

        public bool CodexLaunchStarted { get; private set; }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await InspectCodexAvailabilityAsync();
        }

        private async Task InspectCodexAvailabilityAsync()
        {
            CodexStatusText.Foreground = FindResource("SecondaryTextBrush") as Brush ?? Brushes.DimGray;
            CodexStatusText.Text = DesktopPetText.DetectingCodex;
            try
            {
                _codexAvailability = await Task.Run(DesktopPetCodexService.InspectAvailability);
                CodexStatusText.Foreground = _codexAvailability.IsAvailable
                    ? FindResource("SecondaryTextBrush") as Brush ?? Brushes.DimGray
                    : Brushes.Firebrick;
                CodexStatusText.Text = _codexAvailability.Status;
            }
            catch (Exception ex)
            {
                _codexAvailability = null;
                CodexStatusText.Foreground = Brushes.Firebrick;
                CodexStatusText.Text = DesktopPetText.Format("CodexCheckFailed", ex.Message);
            }
            finally
            {
                UpdatePrimaryAction();
            }
        }

        private void CreationModeTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(e.Source, CreationModeTabControl))
                UpdatePrimaryAction();
        }

        private void UpdatePrimaryAction()
        {
            if (PrimaryButton == null)
                return;

            var useCodex = CreationModeTabControl.SelectedIndex == 0;
            PrimaryButton.Content = useCodex ? DesktopPetText.CreateInCodex : DesktopPetText.ImportAndSelect;
            PrimaryButton.IsEnabled = !_isCreating && (!useCodex || _codexAvailability?.IsAvailable == true);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = DesktopPetText.SelectSpriteSheet,
                Filter = DesktopPetText.SpriteSheetFilter,
                CheckFileExists = true,
                Multiselect = false,
            };

            if (dialog.ShowDialog(this) != true)
                return;

            SpriteSheetPathTextBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(PetNameTextBox.Text))
                PetNameTextBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }

        private async void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCreating)
                return;

            if (CreationModeTabControl.SelectedIndex == 0)
                await LaunchCodexAsync();
            else
                await ImportSpriteSheetAsync();
        }

        private async Task LaunchCodexAsync()
        {
            _isCreating = true;
            UpdatePrimaryAction();
            CodexStatusText.Foreground = FindResource("SecondaryTextBrush") as Brush ?? Brushes.DimGray;
            CodexStatusText.Text = DesktopPetText.PreparingCodex;
            try
            {
                await DesktopPetCodexService.LaunchAsync(CodexConceptTextBox.Text);
                CodexLaunchStarted = true;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                CodexStatusText.Foreground = Brushes.Firebrick;
                CodexStatusText.Text = DesktopPetText.Format("CodexOpenFailed", ex.Message);
            }
            finally
            {
                _isCreating = false;
                UpdatePrimaryAction();
            }
        }

        private async Task ImportSpriteSheetAsync()
        {
            var version = SpriteVersionComboBox.SelectedItem is ComboBoxItem { Tag: string versionText }
                && int.TryParse(versionText, out var selectedVersion)
                    ? selectedVersion
                    : 2;

            _isCreating = true;
            UpdatePrimaryAction();
            ImportStatusText.Foreground = Brushes.DimGray;
            ImportStatusText.Text = DesktopPetText.Importing;
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
                ImportStatusText.Foreground = Brushes.Firebrick;
                ImportStatusText.Text = ex.Message;
            }
            finally
            {
                _isCreating = false;
                UpdatePrimaryAction();
            }
        }
    }
}
