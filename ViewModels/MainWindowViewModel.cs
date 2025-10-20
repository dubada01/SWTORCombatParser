using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.Notes;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.BattleReview;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.DataGrid;
using SWTORCombatParser.ViewModels.Death_Review;
using SWTORCombatParser.ViewModels.HistoricalLogs;
using SWTORCombatParser.ViewModels.Home_View_Models;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Overviews;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.Battle_Review;
using SWTORCombatParser.Views.DataGrid_Views;
using SWTORCombatParser.Views.Home_Views;
using SWTORCombatParser.Views.Home_Views.PastCombatViews;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Overviews;
using SWTORCombatParser.Views.Phases;
using SWTORCombatParser.Views.SettingsView;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MsBox.Avalonia;
using ReactiveUI;
using SWTORCombatParser.DataStructures.Phases;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Death_Review;
using SWTORCombatParser.Views.Timers;

namespace SWTORCombatParser.ViewModels
{
    public class MainWindowViewModel :ReactiveObject
    {
        private readonly CombatMonitorViewModel _combatMonitorViewModel;
        private Entity localEntity;
        private string parselyLink = "https://parsely.io/";
        private bool canOpenParsely;
        private SolidColorBrush uploadButtonBackground = new SolidColorBrush(Colors.WhiteSmoke);

        private readonly Dictionary<Guid, HistoricalCombatViewModel> _activeHistoricalCombatOverviews = new Dictionary<Guid, HistoricalCombatViewModel>();
        private int selectedTabIndex;
        
        public string Title { get; set; }
        public ObservableCollection<TabInstance> ContentTabs { get; set; } = new ObservableCollection<TabInstance>();
        public PastCombatsView PastCombatsView { get; set; }

        public PhaseBar PhasesBar { get; set; }
        public Combat CurrentlyDisplayedCombat { get; set; }
        public Combat UnfilteredDisplayedCombat { get; set; }
        private bool _allViewsUpToDate;
        private int activeRowSpan;
        private TabInstance _selectedTab;
        private bool _logLoaded;

        public TabInstance SelectedTab
        {
            get => _selectedTab;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTab, value);
                foreach (var tabInstance in ContentTabs)
                {
                    tabInstance.Unselect();
                }
                _selectedTab.Select();
            }
        }

        public int SelectedTabIndex
        {
            get => selectedTabIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref selectedTabIndex, value);
            }
            
        }

        public int ActiveRowSpan
        {
            get => activeRowSpan;
            set => this.RaiseAndSetIfChanged(ref activeRowSpan, value);
        
        }

        public MainWindowViewModel(HotkeyHandler hotkeyHandler)
        {
            HotkeyHandler = hotkeyHandler;
            Leaderboards.Init();

            Title = $"HMB Racer Tool v{Assembly.GetExecutingAssembly().GetName().Version}";
            
            MetricColorLoader.Init();
            DefaultPhaseLoader.LoadBuiltinPhases();
            ClassIdentifier.InitializeAvailableClasses();
            EncounterLoader.LoadAllEncounters();


            _combatMonitorViewModel = new CombatMonitorViewModel();
            CombatSelectionMonitor.CombatSelected += SelectCombat;
            Observable.FromEvent<double>(
                manager => _combatMonitorViewModel.OnNewLogTimeOffsetMs += manager,
                manager => _combatMonitorViewModel.OnNewLogTimeOffsetMs -= manager).Buffer(TimeSpan.FromSeconds(2)).Subscribe(UpdateLogTimeOffset);
            Observable.FromEvent<double>(
    manager => _combatMonitorViewModel.OnNewTotalTimeOffsetMs += manager,
    manager => _combatMonitorViewModel.OnNewTotalTimeOffsetMs -= manager).Buffer(TimeSpan.FromSeconds(2)).Subscribe(UpdateTotalTimeOffset);
            Observable.FromEvent<Combat>(
                manager => _combatMonitorViewModel.OnLiveCombatUpdate += manager,
                manager => _combatMonitorViewModel.OnLiveCombatUpdate -= manager).Sample(TimeSpan.FromSeconds(2)).Subscribe(UpdateCombat);
            _combatMonitorViewModel.OnMonitoringStateChanged += MonitoringStarted;
            _combatMonitorViewModel.LocalPlayerId += LocalPlayerChanged;

            PastCombatsView = new PastCombatsView(_combatMonitorViewModel);

        }

        public SolidColorBrush UploadButtonBackground
        {
            get => uploadButtonBackground; set => this.RaiseAndSetIfChanged(ref uploadButtonBackground, value);
        }
        public ReactiveCommand<Unit,Unit> OpenSettingsWindowCommand => ReactiveCommand.Create(OpenSettingsWindow);

        private void OpenSettingsWindow()
        {
            HotkeyHandler.UnregAll();
            var settingsWindow = new SettingsWindow();
            //settingsWindow.Owner = App.Current.MainWindow;
            settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            settingsWindow.Closing += (e,s) => 
            {
                HotkeyHandler.UpdateKeys();
            };
            // Check if we're using the ClassicDesktop style and retrieve the handle
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    settingsWindow.ShowDialog(desktop.MainWindow);
                });
            }
        }

        public bool LogLoaded
        {
            get => _logLoaded;
            set => this.RaiseAndSetIfChanged(ref _logLoaded, value);
        }
        
        public void ShowDeathReviewForCombat(Combat viewModelCombat)
        {
        }

        public ReactiveCommand<Unit,Unit> OpenParselyConfigCommand => ReactiveCommand.Create(OpenParselyConfig);

        private void OpenParselyConfig()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var parselySettingsWindow = new ParselySettings();
                parselySettingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                parselySettingsWindow.ShowDialog(desktop.MainWindow);
            }
        }

        public HotkeyHandler HotkeyHandler { get; internal set; }

        private void HandleParselyUploadStart()
        {
            UploadButtonBackground = new SolidColorBrush(Colors.CornflowerBlue);
        }
        private async void UploadToParsely()
        {
            var response = await ParselyUploader.UploadCurrentCombat(_combatMonitorViewModel.GetActiveFile());
            if (!string.IsNullOrEmpty(response))
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var box = MessageBoxManager.GetMessageBoxStandard("Error", response);
                    await box.ShowWindowDialogAsync(desktop.MainWindow);
                }
            }
            else
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var box = MessageBoxManager.GetMessageBoxStandard("Success",
                        "Successfully uploaded to Parsely! Use the File->Parsely menu in Orbs to open and view your battle.",
                        windowStartupLocation: WindowStartupLocation.CenterOwner);
                    await box.ShowWindowDialogAsync(desktop.MainWindow);
                }
            }
        }



        private void MonitoringStarted(bool state)
        {
        }

        private void UpdateLogTimeOffset(IList<double> logOffsetFor2Seconds)
        {
            if (!logOffsetFor2Seconds.Any())
                return;
            var average = logOffsetFor2Seconds.Average() / 1000d;
            _combatMonitorViewModel.CurrentLogOffsetMs = Math.Round(average, 1);
        }
        private void UpdateTotalTimeOffset(IList<double> logOffsetFor2Seconds)
        {
            if (!logOffsetFor2Seconds.Any())
                return;
            var average = logOffsetFor2Seconds.Average() / 1000d;
            _combatMonitorViewModel.CurrentTotalOffsetMs = Math.Round(average, 1);
        }
        private void UpdateCombat(Combat updatedCombat)
        {
            CurrentlyDisplayedCombat = updatedCombat;
            if (LoadingWindowFactory.MainWindowHidden)
                return;
            try
            {
                Dispatcher.UIThread.Invoke(delegate
                {
                });
                _allViewsUpToDate = false;
            }
            catch (Exception e)
            {
                Logging.LogError("Failed to update combat visuals: " + e.Message + "\r\n" + e.StackTrace);
            }
        }
        private void SelectCombat(Combat selectedCombat)
        {
            LogLoaded = true;
            UnfilteredDisplayedCombat = selectedCombat;
            UpdateViewsWithSelectedCombat(selectedCombat);
        }
        private void UpdateViewsWithSelectedCombat(Combat selectedCombat)
        {
            try
            {
                Dispatcher.UIThread.Invoke(delegate
                {
                    CurrentlyDisplayedCombat = selectedCombat;
                    _allViewsUpToDate = true;
                });
            }
            catch (Exception e)
            {
                Logging.LogError("Failed to update combat visuals: " + e.Message + "\r\n" + e.StackTrace);
            }
        }

        private void LocalPlayerChanged(Entity obj)
        {
            if (localEntity == obj)
                return;
            Dispatcher.UIThread.Invoke(delegate
            {
                if (localEntity != obj)
                {
                }
                localEntity = obj;
            });
        }
    }
}
