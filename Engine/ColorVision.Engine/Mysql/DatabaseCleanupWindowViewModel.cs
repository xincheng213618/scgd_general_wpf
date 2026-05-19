using ColorVision.Common.MVVM;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Database
{
    public sealed class DatabaseCleanupSourceViewModel : ViewModelBase
    {
        private readonly IDatabaseCleanupSourceProvider _provider;

        private string _description = string.Empty;
        private string _keepMonthsText = "3";
        private string _status = "打开窗口后会自动统计。";
        private bool _isBusy;

        public DatabaseCleanupSourceViewModel(IDatabaseCleanupSourceProvider provider)
        {
            _provider = provider;
            _description = provider.Description;
            RefreshCommand = new RelayCommand(_ => _ = RefreshAsync(), _ => !IsBusy);
            CleanupHistoryCommand = new RelayCommand(_ => ExecuteCleanupHistory(), _ => !IsBusy);
            CleanupAllCommand = new RelayCommand(_ => ExecuteCleanupAll(), _ => !IsBusy);
        }

        public string SourceId => _provider.Id;
        public string DisplayName => _provider.DisplayName;
        public int Order => _provider.Order;
        public ObservableCollection<DatabaseCleanupTableInfo> Tables { get; } = new();

        public RelayCommand RefreshCommand { get; }
        public RelayCommand CleanupHistoryCommand { get; }
        public RelayCommand CleanupAllCommand { get; }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public string KeepMonthsText
        {
            get => _keepMonthsText;
            set
            {
                _keepMonthsText = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public Task RefreshAsync()
        {
            if (IsBusy)
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                SetBusy(true);
                SetStatus("正在统计表数据...");

                try
                {
                    SetDescription(_provider.Description);
                    var snapshot = _provider.LoadTables();
                    ApplySnapshot(snapshot);

                    int existingCount = snapshot.Count(item => item.Exists);
                    SetStatus(existingCount > 0
                        ? $"已加载 {existingCount:N0} 张可清理数据表。"
                        : "当前没有找到可清理数据表。");
                }
                catch (Exception ex)
                {
                    SetStatus("加载统计失败。"
                    );
                    ShowMessage($"{DisplayName} 统计失败：{ex.Message}", MessageBoxImage.Error);
                }
                finally
                {
                    SetBusy(false);
                }
            });
        }

        private void ExecuteCleanupHistory()
        {
            if (!TryGetKeepMonths(out int keepMonths))
            {
                ShowMessage("保留月数必须是大于 0 的整数。", MessageBoxImage.Warning);
                return;
            }

            string tableList = BuildExistingTableList();
            var confirmMessage = string.IsNullOrWhiteSpace(tableList)
                ? $"将保留最近 {keepMonths} 个月的数据，继续执行 {DisplayName} 历史清理吗？"
                : $"将保留最近 {keepMonths} 个月的数据，并清理以下数据表的历史数据：{Environment.NewLine}{tableList}{Environment.NewLine}{Environment.NewLine}是否继续？";

            bool confirmed = false;
            RunOnUi(() =>
            {
                confirmed = MessageBox1.Show(Application.Current.GetActiveWindow(), confirmMessage, DisplayName, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
            });

            if (!confirmed)
                return;

            _ = Task.Run(() => ExecuteCleanup(() => _provider.CleanupHistory(keepMonths), $"正在清理 {DisplayName} 历史数据..."));
        }

        private void ExecuteCleanupAll()
        {
            string tableList = BuildExistingTableList();
            var confirmMessage = string.IsNullOrWhiteSpace(tableList)
                ? $"将清空 {DisplayName} 的全部数据，是否继续？"
                : $"将清空以下数据表的全部数据：{Environment.NewLine}{tableList}{Environment.NewLine}{Environment.NewLine}是否继续？";

            bool confirmed = false;
            RunOnUi(() =>
            {
                confirmed = MessageBox1.Show(Application.Current.GetActiveWindow(), confirmMessage, DisplayName, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
            });

            if (!confirmed)
                return;

            _ = Task.Run(() => ExecuteCleanup(_provider.CleanupAll, $"正在清空 {DisplayName} 数据..."));
        }

        private void ExecuteCleanup(Func<DatabaseCleanupExecutionResult> action, string busyStatus)
        {
            SetBusy(true);
            SetStatus(busyStatus);

            try
            {
                var result = action();
                SetDescription(_provider.Description);
                ApplySnapshot(_provider.LoadTables());
                SetStatus(result.StatusMessage);

                string message = result.SummaryLines.Count > 0
                    ? string.Join(Environment.NewLine, result.SummaryLines)
                    : result.StatusMessage;
                ShowMessage(message, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("清理失败。");
                ShowMessage($"{DisplayName} 清理失败：{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private bool TryGetKeepMonths(out int keepMonths)
        {
            return int.TryParse(KeepMonthsText, out keepMonths) && keepMonths > 0;
        }

        private string BuildExistingTableList()
        {
            return string.Join(Environment.NewLine, Tables.Where(item => item.Exists).Select(item => $"- {item.TableName}"));
        }

        private void ApplySnapshot(IReadOnlyList<DatabaseCleanupTableInfo> snapshot)
        {
            RunOnUi(() =>
            {
                Tables.Clear();
                foreach (var item in snapshot)
                {
                    Tables.Add(item);
                }
            });
        }

        private void SetDescription(string description) => RunOnUi(() => Description = description);
        private void SetStatus(string status) => RunOnUi(() => Status = status);
        private void SetBusy(bool isBusy) => RunOnUi(() => IsBusy = isBusy);

        private void ShowMessage(string message, MessageBoxImage image)
        {
            RunOnUi(() =>
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), message, DisplayName, MessageBoxButton.OK, image);
            });
        }

        private static void RunOnUi(Action action)
        {
            if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
            {
                action();
                return;
            }

            Application.Current.Dispatcher.Invoke(action);
        }
    }

    public sealed class DatabaseCleanupWindowViewModel : ViewModelBase
    {
        private DatabaseCleanupSourceViewModel? _selectedSource;

        public ObservableCollection<DatabaseCleanupSourceViewModel> Sources { get; } = new();

        public DatabaseCleanupSourceViewModel? SelectedSource
        {
            get => _selectedSource;
            set
            {
                _selectedSource = value;
                OnPropertyChanged();
            }
        }

        public DatabaseCleanupWindowViewModel()
        {
            AssemblyHandler.GetInstance().RefreshAssemblies();

            var providers = AssemblyHandler.GetInstance()
                .LoadImplementations<IDatabaseCleanupSourceProvider>()
                .OrderBy(provider => provider.Order)
                .ThenBy(provider => provider.DisplayName)
                .ToList();

            foreach (var provider in providers)
            {
                Sources.Add(new DatabaseCleanupSourceViewModel(provider));
            }

            SelectedSource = Sources.FirstOrDefault();
        }

        public Task RefreshAllAsync()
        {
            return Task.WhenAll(Sources.Select(source => source.RefreshAsync()));
        }
    }
}