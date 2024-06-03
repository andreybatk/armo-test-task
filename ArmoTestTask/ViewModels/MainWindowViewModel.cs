using ArmoTestTask.Infrastructure.Commands;
using ArmoTestTask.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ArmoTestTask.ViewModels
{
    public class MainWindowViewModel : ViewModel
    {
        private string _startFolder;
        private string _searchPattern;
        private string _currentFolder;
        private string _elapsedTime;
        private int _foundFiles;
        private int _totalFiles;
        private int _currentIndex;
        private bool _isRunning;
        private Stopwatch _stopwatch;
        private DispatcherTimer _timer;
        private ObservableCollection<string> _searchList;
        private CancellationTokenSource _cts;

        public MainWindowViewModel()
        {
            LoadSettings();
            StartSearch = new RelayCommand(async (x) => await OnStartSearchCommandExecuted(x), CanStartSearchCommandExecute);
            StopSearch = new RelayCommand((x) => OnStopSearchCommandExecuted(x), CanStopSearchCommandExecute);
            ContinueSearch = new RelayCommand(async (x) => await OnContinueSearchCommandExecuted(x), CanContinueSearchCommandExecute);
        }

        public ICommand StartSearch { get; }
        public ICommand StopSearch { get; }
        public ICommand ContinueSearch { get; }
        public string StartFolder { get => _startFolder; set => Set(ref _startFolder, value); }
        public string SearchPattern { get => _searchPattern; set => Set(ref _searchPattern, value); }
        public string CurrentFolder { get => _currentFolder; set => Set(ref _currentFolder, value); }
        public string ElapsedTime { get => _elapsedTime; set => Set(ref _elapsedTime, value); }
        public int TotalFiles { get => _totalFiles; set => Set(ref _totalFiles, value); }
        public int FoundFiles { get => _foundFiles; set => Set(ref _foundFiles, value); }
        public bool IsRunning { get => _isRunning; set => Set(ref _isRunning, value); }
        public ObservableCollection<string> SearchList { get => _searchList; set => Set(ref _searchList, value); }

        private bool CanStartSearchCommandExecute(object p)
        {
            return !IsRunning;
        }
        private bool CanStopSearchCommandExecute(object p)
        {
            return IsRunning;
        }
        private bool CanContinueSearchCommandExecute(object p)
        {
            return !IsRunning;
        }
        private void OnStopSearchCommandExecuted(object p)
        {
            _cts.Cancel();
            IsRunning = false;
        }
        private async Task OnContinueSearchCommandExecuted(object p)
        {
            _cts = new CancellationTokenSource();
            IsRunning = true;

            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _timer.Tick += (s, e) => ElapsedTime = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss\.fff");
            _stopwatch.Start();
            _timer.Start();

            var files = EnumerateAllFiles(StartFolder, SearchPattern, _currentIndex);
            try
            {
                await Task.Run(() =>
                {
                    foreach (var file in files)
                    {
                        _cts.Token.ThrowIfCancellationRequested();
                        FoundFiles++;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SearchList.Add(file);
                            _currentIndex++;
                        });
                    }
                });
            }
            catch (OperationCanceledException)
            {
                _stopwatch.Stop();
                _timer.Stop();
                MessageBox.Show("Поиск отменен.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Поиск закончился с ошибкой: {ex}", "Внимание", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsRunning = false;
            }
        }
        private async Task OnStartSearchCommandExecuted(object p)
        {
            SaveSettings();
            _cts = new CancellationTokenSource();
            SearchList = new ObservableCollection<string>();
            IsRunning = true;

            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _timer.Tick += (s, e) => ElapsedTime = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss\.fff");
            _stopwatch.Start();
            _timer.Start();

            FoundFiles = 0;
            TotalFiles = 0;
            _currentIndex = 0;

            var files = EnumerateAllFiles(StartFolder, SearchPattern, _currentIndex);
            try
            {
                await Task.Run(() =>
                {
                    foreach (var file in files)
                    {
                        _cts.Token.ThrowIfCancellationRequested();
                        FoundFiles++;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SearchList.Add(file);
                            _currentIndex++;
                        });
                    }
                });
            }
            catch (OperationCanceledException e) when (e.CancellationToken == _cts.Token)
            {
                _stopwatch.Stop();
                _timer.Stop();
                MessageBox.Show("Поиск отменен.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Поиск закончился ошибкой {ex}", "Внимание", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsRunning = false;
            }
        }
        private void SaveSettings()
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var section = config.GetSection("searchSettings") as AppSettingsSection;

            if (section == null)
            {
                section = new AppSettingsSection();
                config.Sections.Add("searchSettings", section);
            }

            section.Settings["folderPath"].Value = StartFolder;
            section.Settings["searchPattern"].Value = SearchPattern;

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("searchSettings");
        }
        private void LoadSettings()
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var section = config.GetSection("searchSettings") as AppSettingsSection;

            StartFolder = section.Settings["folderPath"].Value;
            SearchPattern = section.Settings["searchPattern"].Value;
        }
        private IEnumerable<string> EnumerateAllFiles(string path, string pattern, int skipIndex)
        {
            _cts.Token.ThrowIfCancellationRequested();

            IEnumerable<string> files = null;
            try
            {
                files = Directory.EnumerateFiles(path, $"*{pattern}*").Skip(skipIndex);
                TotalFiles += Directory.GetFiles(path).Count();
            }
            catch { }

            if (files != null)
            {
                foreach (var file in files)
                {
                    yield return file;
                }
            }

            IEnumerable<string> directories = null;
            try
            {
                directories = Directory.EnumerateDirectories(path);
                CurrentFolder = directories.FirstOrDefault();
            }
            catch { }

            if (directories != null)
            {
                foreach (var file in directories.SelectMany(d => EnumerateAllFiles(d, $"*{pattern}*", _currentIndex)))
                {
                    yield return file;
                }
            }
        }
    }
}