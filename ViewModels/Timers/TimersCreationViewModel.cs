using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.Timers.Boss_Timers;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.Timers.HOT_Timers;
using SWTORCombatParser.DataStructures.Timers.Defensive_Timers;
using SWTORCombatParser.DataStructures.Timers.Offensive_Timers;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Encounter_Selection;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;
using SWTORCombatParser.DataStructures.AbilityInfo;
using Timer = SWTORCombatParser.DataStructures.Timer;
using SWTORCombatParser.DataStructures.ChallengeInfo;

namespace SWTORCombatParser.ViewModels.Timers
{
    public enum TimerType
    {
        Encounter,
        Discipline
    }
    public class TimersCreationViewModel : ReactiveObject
    {
        private TimersWindowViewModel _disciplineTimersWindow;
        private TimersWindowViewModel _encounterTimersWindow;
        private AlertsWindowViewModel _alertTimersWindow;
        private EncounterSelectionViewModel _enounterSelectionViewModel;
        private TimerType selectedTimerSourceType = TimerType.Discipline;
        private bool _isLocked;
        private Timer _timerEdited;
        private bool disciplineTimersActive;
        private bool alertTimersActive;
        public event Action DisciplineClosed = delegate { };
        public event Action AlertsClosed = delegate { };
        public event Action EncounterClosed = delegate { };

        private List<DefaultTimersData> _savedTimersData = new List<DefaultTimersData>();
        public EncounterSelectionView EncounterSelectionView
        {
            get => _encounterSelectionView;
            set
            {
                if (Equals(value, _encounterSelectionView)) return;
                this.RaiseAndSetIfChanged(ref _encounterSelectionView, value);
            }
        }

        public void UpdateSelectedEncounter(string encounterName, string bossName)
        {
            SelectedTimerSource = encounterName + "|" + bossName;
        }

        public List<TimerType> TimerSourcesTypes
        {
            get => _timerSourcesTypes;
            set
            {
                if (Equals(value, _timerSourcesTypes)) return;
                this.RaiseAndSetIfChanged(ref _timerSourcesTypes, value);
            }
        }

        private string selectedTimerSource;

        public TimerType SelectedTimerSourceType
        {
            get => selectedTimerSourceType; set
            {
                this.RaiseAndSetIfChanged(ref selectedTimerSourceType, value);
                this.RaisePropertyChanged(nameof(DisciplineTimerSelected));
                if (selectedTimerSourceType == TimerType.Encounter)
                {
                    AvailableTimerSources = EncounterTimersList;
                    var currentSelection = _enounterSelectionViewModel.GetCurrentSelection();
                    UpdatePreSelectedEncounter();
                }
                if (selectedTimerSourceType == TimerType.Discipline)
                {
                    AvailableTimerSources = DisciplineTimersList;
                    var mostRecentDiscipline = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(TimeUtility.CorrectedTime);
                    if (mostRecentDiscipline != null && mostRecentDiscipline.Discipline != null)
                    {
                        var source = DisciplineTimersList.FirstOrDefault(v => v.Contains(mostRecentDiscipline.Discipline));
                        if (!string.IsNullOrEmpty(source))
                        {
                            SelectedTimerSource = source;
                        }

                    }
                    else
                    {
                        SelectedTimerSource = "Shared";
                    }
                }
                this.RaisePropertyChanged(nameof(VisibleTimerSelected));
            }
        }

        private void UpdatePreSelectedEncounter()
        {
            if (CombatIdentifier.CurrentCombat != null && CombatIdentifier.CurrentCombat.IsCombatWithBoss)
            {
                _enounterSelectionViewModel.SelectedEncounter = _enounterSelectionViewModel.AvailableEncounters.FirstOrDefault(e => e.Name == CombatIdentifier.CurrentCombat.ParentEncounter.Name);
                _enounterSelectionViewModel.SelectedBoss = CombatIdentifier.CurrentCombat.EncounterBossDifficultyParts.Item1;
            }
            UpdateSelectedEncounter(_enounterSelectionViewModel.SelectedEncounter.Name, _enounterSelectionViewModel.SelectedBoss);
        }

        public bool DisciplineTimerSelected => SelectedTimerSourceType == TimerType.Discipline;

        public bool VisibleTimerSelected => DisciplineTimerSelected && SelectedTimerSource != "Shared" &&
                                            SelectedTimerSource != "HOTS" && SelectedTimerSource != "DOTS" && SelectedTimerSource != "DCD" && SelectedTimerSource != "OCD";

        public List<string> AvailableTimerSources
        {
            get => _availableTimerSources;
            set
            {
                if (Equals(value, _availableTimerSources)) return;
                this.RaiseAndSetIfChanged(ref _availableTimerSources, value);
            }
        }

        public string SelectedTimerSource
        {
            get => selectedTimerSource; set
            {
                if(string.IsNullOrEmpty(value))
                    return;
                this.RaiseAndSetIfChanged(ref selectedTimerSource, value);
                this.RaisePropertyChanged(nameof(VisibleTimerSelected));
                UpdateTimerRows();
            }
        }

        public List<string> EncounterTimersList
        {
            get => _encounterTimersList;
            set
            {
                if (Equals(value, _encounterTimersList)) return;
                this.RaiseAndSetIfChanged(ref _encounterTimersList, value);
            }
        }

        public List<string> DisciplineTimersList
        {
            get => _disciplineTimersList;
            set
            {
                if (Equals(value, _disciplineTimersList)) return;
                this.RaiseAndSetIfChanged(ref _disciplineTimersList, value);
            }
        }

        public bool EncounterTimersActive
        {
            get => _encounterTimersActive;
            set
            {
                this.RaiseAndSetIfChanged(ref _encounterTimersActive, value);
                _encounterTimersWindow.Active = value;
            }
        }

        public bool DisciplineTimersActive
        {
            get => disciplineTimersActive; set
            {
                if (value == disciplineTimersActive)
                    return;
                this.RaiseAndSetIfChanged(ref disciplineTimersActive, value);
                _disciplineTimersWindow.Active =  value;
            }
        }

        public bool AlertsActive
        {
            get => alertTimersActive;
            set
            {
                this.RaiseAndSetIfChanged(ref alertTimersActive, value);
                _alertTimersWindow.Active = value;
            }
        }

        public void TryShow()
        {
            if (DisciplineTimersActive)
                _disciplineTimersWindow.ShowOverlayWindow();
        }
        public void HideTimers()
        {
            _disciplineTimersWindow.HideOverlayWindow();
        }

        public ObservableCollection<TimerRowInstanceViewModel> TimerRows
        {
            get => _timerRows;
            set
            {
                if (Equals(value, _timerRows)) return;
                this.RaiseAndSetIfChanged(ref _timerRows, value);
            }
        }
        public void RefreshEncounterSelection()
        {
            if (selectedTimerSourceType == TimerType.Encounter)
                UpdatePreSelectedEncounter();
        }
        public TimersCreationViewModel()
        {
            Task.Run(() =>
            {
                AbilityLoader.SetAbsorbAbilities();
                ChallengeLoader.TryLoadChallenges();
                BossTimerLoader.TryLoadBossTimers();
                HotTimerLoader.TryLoadHots();
                DotTimerLoader.TryLoadDots();
                OffensiveTimerLoader.TryLoadOffensives();
                DefensiveTimerLoader.TryLoadDefensives();
                TimerController.RefreshAvailableTimers();
                RefreshAvaialbleTriggerOwners();
                if (DisciplineTimersList.Count > 0)
                {
                    SelectedTimerSource = DisciplineTimersList[0];
                }
            });

            CombatLogStreamer.CombatUpdated += status =>
            {
                if (status.Type == UpdateType.Start)
                    CanChangeAudio = false;
                if (status.Type == UpdateType.Stop)
                    CanChangeAudio = true;
            };
            EncounterSelectionView = EncounterSelectionFactory.GetEncounterSelectionView(false);
            _enounterSelectionViewModel = EncounterSelectionView.DataContext as EncounterSelectionViewModel;
            _enounterSelectionViewModel.SelectionUpdated += UpdateSelectedEncounter;
            
            _disciplineTimersWindow = new DisciplineTimersWindowViewModel("Discipline");
            _disciplineTimersWindow.CloseRequested += () =>
            {
                DisciplineTimersActive = false;
                DisciplineClosed();
            };
            disciplineTimersActive = _disciplineTimersWindow.Active;
            
            _alertTimersWindow = new AlertsWindowViewModel("Alerts");
            _alertTimersWindow.CloseRequested += () =>
            {
                AlertsActive = false;
                AlertsClosed();
            };
            alertTimersActive = _alertTimersWindow.Active;
            
            _encounterTimersWindow = new EncounterTimerWindowViewModel("Encounter");
            _encounterTimersWindow.CloseRequested += () =>
            {
                EncounterTimersActive = false;
                EncounterClosed();
            };
            _encounterTimersActive = _encounterTimersWindow.Active;
            
            CombatLogStateBuilder.PlayerDiciplineChanged += SetClass;
            CombatLogStreamer.HistoricalLogsFinished += SetDiscipline;
        }

        private void SetDiscipline(DateTime combatEndTime, bool localPlayerIdentified)
        {
            if (!CombatMonitorViewModel.IsLiveParseActive() || !localPlayerIdentified)
                return;
            var mostRecentDiscipline = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(combatEndTime);
            if (mostRecentDiscipline == null)
                return;
            var source = DisciplineTimersList.FirstOrDefault(v => v.Contains(mostRecentDiscipline.Discipline));
            if (source == null)
            {
                source = mostRecentDiscipline.Discipline;
                DisciplineTimersList.Add(source);
                DefaultOrbsTimersManager.UpdateTimersActive(false, source);
            }
            if (!string.IsNullOrEmpty(source))
            {
                SelectedTimerSource = source;
            }

        }
        private void RefreshAvaialbleTriggerOwners()
        {
            _savedTimersData = DefaultOrbsTimersManager.GetAllDefaults();
            if (_savedTimersData.All(t => t.TimerSource != "Shared"))
            {
                _savedTimersData.Add(new DefaultTimersData() { TimerSource = "Shared" });
                DefaultOrbsTimersManager.SetSavedTimers(new List<Timer>(), "Shared");
            }
            var savedTimerSources = new List<string>(_savedTimersData.Where(t => t.TimerSource != null && !t.TimerSource.Contains('|')).Select(t => t.TimerSource));
            savedTimerSources = savedTimerSources.OrderBy(s => s).ToList();
            savedTimerSources.SwapItems(0, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "Shared")));
            savedTimerSources.SwapItems(1, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "HOTS")));
            savedTimerSources.SwapItems(2, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "DOTS")));
            savedTimerSources.SwapItems(3, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "DCD")));
            savedTimerSources.SwapItems(4, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "OCD")));

            DisciplineTimersList = savedTimerSources;
        }

        public ReactiveCommand<object,Unit> CreateNewTimerCommand => ReactiveCommand.Create<object>(CreateNewTimer);

        private void CreateNewTimer(object obj)
        {
            var vm = new ModifyTimerViewModel(SelectedTimerSource);
            vm.OnNewTimer += NewTimer;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var t = new TimerModificationWindow(vm);
                t.ShowDialog(desktop.MainWindow);
            }
        }
        public Bitmap AudioImageSource => !allMuted ? new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/audioIcon.png"))) : new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/mutedIcon.png")));
        public Bitmap VisibilityImageSource => !allHidden ?new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/view.png"))) : new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/hidden.png")));
        private bool allMuted = false;
        private EncounterSelectionView _encounterSelectionView;
        private List<TimerType> _timerSourcesTypes = new List<TimerType> { TimerType.Discipline, TimerType.Encounter };
        private List<string> _availableTimerSources;
        private List<string> _encounterTimersList = new List<string>();
        private List<string> _disciplineTimersList = new List<string>();
        private ObservableCollection<TimerRowInstanceViewModel> _timerRows = new ObservableCollection<TimerRowInstanceViewModel>();
        private bool _canChangeAudio = true;
        private string _importId;
        private bool allActive;
        private bool allHidden;
        private bool _encounterTimersActive;

        public bool AllActive
        {
            get => allActive; set
            {
                this.RaiseAndSetIfChanged(ref allActive, value);
                if (TimerRows.Count == 0)
                    return;
                Task.Run(() =>
                {
                    Parallel.ForEach(TimerRows, timer =>
                    {
                        timer.SetActive(allActive);
                    });
                    DefaultOrbsTimersManager.SetTimersEnabledForSource(TimerRows.Select(t => t.SourceTimer).ToList(), TimerRows.First().SourceTimer.TimerSource);
                    TimerController.RefreshAvailableTimers();
                });
            }
        }
        public bool CanChangeAudio
        {
            get => _canChangeAudio;
            set
            {
                if (value == _canChangeAudio) return;
                this.RaiseAndSetIfChanged(ref _canChangeAudio, value);
            }
        }
        public ReactiveCommand<object,Unit> ToggleVisibilityCommand => ReactiveCommand.Create<object>(ToggleVisibility);

        private void SetVisibilityIcon(bool status)
        {
            allHidden = status;
            this.RaisePropertyChanged(nameof(VisibilityImageSource));
        }
        private void ToggleVisibility(object obj)
        {
            SetVisibilityIcon(!allHidden);
            if (TimerRows.Count == 0)
                return;
            Task.Run(() =>
            {
                Parallel.ForEach(TimerRows, timer =>
                {
                    timer.SetVisibility(allHidden);
                });
                DefaultOrbsTimersManager.SetTimersVisibilityForSource(TimerRows.Select(t => t.SourceTimer).ToList(), TimerRows.First().SourceTimer.TimerSource);
                TimerController.RefreshAvailableTimers();
            });
        }
        public ReactiveCommand<object,Unit> ToggleAudioCommand => ReactiveCommand.Create<object>(ToggleAudio);

        private void SetAudioIcon(bool status)
        {
            allMuted = status;
            this.RaisePropertyChanged(nameof(AudioImageSource));
        }
        private void ToggleAudio(object obj)
        {
            SetAudioIcon(!allMuted);
            if (TimerRows.Count == 0)
                return;
            Task.Run(() =>
            {
                Parallel.ForEach(TimerRows, timer =>
                {
                    timer.SetAudio(allMuted);
                });
                DefaultOrbsTimersManager.SetTimersAudioForSource(TimerRows.Select(t => t.SourceTimer).ToList(), TimerRows.First().SourceTimer.TimerSource);
                TimerController.RefreshAvailableTimers();
            });
        }

        public string ImportId
        {
            get => _importId;
            set
            {
                if (value == _importId) return;
                this.RaiseAndSetIfChanged(ref _importId, value);
            }
        }

        public ReactiveCommand<object,Unit> ImportCommand => ReactiveCommand.Create<object>(Import);

        private async void Import(object obj)
        {
            if (TimerRows.Any(t => t.SourceTimer.ShareId == ImportId))
                return;
            var timer = await TimerDatabaseAccess.GetTimerFromId(ImportId);
            if (timer == null)
                return;
            timer.IsEnabled = true;
            timer.TimerSource = selectedTimerSource;
            NewTimer(timer, false, true);
        }

        private void CancelEdit(Timer editedTimer)
        {
            var addedBack = new TimerRowInstanceViewModel() { SourceTimer = editedTimer, IsEnabled = editedTimer.IsEnabled };
            addedBack.EditRequested += Edit;
            addedBack.ShareRequested += Share;
            addedBack.DeleteRequested += Delete;
            addedBack.CopyRequested += Copy;
            TimerRows.Add(addedBack);
            UpdateRowColors();
        }

        private void Copy(TimerRowInstanceViewModel obj)
        {
            var copy = obj.SourceTimer.Copy();
            copy.Name = copy.Name + " (COPY)";
            copy.Id = Guid.NewGuid().ToString();
            NewTimer(copy, false);
        }

        private void NewTimer(Timer obj, bool wasEdit, bool wasImport= false)
        {
            if (wasEdit)
                DefaultOrbsTimersManager.RemoveTimerForCharacter(_timerEdited, SelectedTimerSource);
            if (wasImport)
                obj.IsUserAddedTimer = true;
            SaveNewTimer(obj);
            var newTimer = new TimerRowInstanceViewModel() { SourceTimer = obj, IsEnabled = obj.IsEnabled };
            newTimer.EditRequested += Edit;
            newTimer.ShareRequested += Share;
            newTimer.DeleteRequested += Delete;
            newTimer.CopyRequested += Copy;

            TimerRows.Add(newTimer);
            TimerController.RefreshAvailableTimers();
            UpdateRowColors();
        }
        private void SaveNewTimer(Timer timer)
        {
            DefaultOrbsTimersManager.AddTimersForSource(new List<Timer>() { timer }, SelectedTimerSource);

        }
        public void UpdateLock(bool state)
        {
            _disciplineTimersWindow.OverlaysMoveable = !state;
            _alertTimersWindow.OverlaysMoveable = !state;
            _encounterTimersWindow.OverlaysMoveable = !state;
            _isLocked = state;
            if (!_isLocked)
                _alertTimersWindow.ShowOverlayWindow();
            else
                _alertTimersWindow.HideOverlayWindow();
        }
        public void SetClass(Entity player, SWTORClass swtorclass)
        {
            if (!player.IsLocalPlayer || !CombatMonitorViewModel.IsLiveParseActive() || SelectedTimerSource == swtorclass.Discipline || CombatLogStreamer.InCombat)
                return;
            if (!DisciplineTimersList.Contains(swtorclass.Discipline))
            {
                DisciplineTimersList.Add(swtorclass.Discipline);
            }

            SelectedTimerSourceType = TimerType.Discipline;
            SelectedTimerSource = swtorclass.Discipline;

            RefreshAvaialbleTriggerOwners();
        }
        private void UpdateTimerRows()
        {
            _savedTimersData = DefaultOrbsTimersManager.GetAllDefaults();
            var allValidTimers = _savedTimersData.Where(t => SelectedTimerSource.Contains('|') ? CompareEncounters(t.TimerSource, SelectedTimerSource) : t.TimerSource == SelectedTimerSource).ToList();
            List<TimerRowInstanceViewModel> timerObjects = new List<TimerRowInstanceViewModel>();
            if (allValidTimers.Count() == 0)
                timerObjects = new List<TimerRowInstanceViewModel>();
            if (allValidTimers.Count() == 1)
                timerObjects = allValidTimers[0].Timers.Select(t => new TimerRowInstanceViewModel() { SourceTimer = t, IsEnabled = t.IsEnabled }).ToList();
            if (allValidTimers.Count() > 1)
                timerObjects = allValidTimers.SelectMany(s => s.Timers.Select(t => new TimerRowInstanceViewModel() { SourceTimer = t, IsEnabled = t.IsEnabled }).ToList()).ToList();

            timerObjects.ForEach(t => t.EditRequested += Edit);
            timerObjects.ForEach(t => t.ShareRequested += Share);
            timerObjects.ForEach(t => t.CopyRequested += Copy);
            timerObjects.ForEach(t => t.DeleteRequested += Delete);
            timerObjects.ForEach(t => t.ActiveChanged += ActiveChanged);
            TimerRows = new ObservableCollection<TimerRowInstanceViewModel>(timerObjects);
            SetAudioIcon(!TimerRows.Any(t => t.SourceTimer.UseAudio));
            allActive = TimerRows.All(t => t.SourceTimer.IsEnabled);
            UpdateRowColors();
            this.RaisePropertyChanged(nameof(TimerRows));
            this.RaisePropertyChanged(nameof(AllActive));
        }
        private bool CompareEncounters(string encounter1, string encounter2)
        {
            if (!encounter1.Contains("|"))
                return false;
            var parts = encounter1.Split('|');
            var encounterWithoutDiff = string.Join("|", parts[0], parts[1]);
            return encounter2 == encounterWithoutDiff;
        }
        private void Delete(TimerRowInstanceViewModel obj)
        {
            DefaultOrbsTimersManager.RemoveTimerForCharacter(obj.SourceTimer, SelectedTimerSource);
            TimerRows.Remove(obj);
            TimerController.RefreshAvailableTimers();
            UpdateRowColors();
        }

        private async void Share(TimerRowInstanceViewModel obj)
        {
            var currentIds = await TimerDatabaseAccess.GetAllTimerIds();
            string id;
            do
            {
                id = AlphanumericsGenerator.RandomString(3);
            } while (currentIds.Contains(id));
            obj.SourceTimer.ShareId = id;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var shareWindow = new TimerSharePopup(id);
                shareWindow.ShowDialog(desktop.MainWindow);
                DefaultOrbsTimersManager.SetIdForTimer(obj.SourceTimer, SelectedTimerSource, id);
                await TimerDatabaseAccess.AddTimer(obj.SourceTimer);
            }
        }
        private void Edit(TimerRowInstanceViewModel obj)
        {
            _timerEdited = obj.SourceTimer;
            TimerRows.Remove(obj);
            var vm = new ModifyTimerViewModel(SelectedTimerSource);
            vm.OnNewTimer += NewTimer;
            vm.OnCancelEdit += CancelEdit;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var t = new TimerModificationWindow(vm);
                vm.Edit(_timerEdited.Copy());
                t.ShowDialog(desktop.MainWindow);
            }
        }
        private void ActiveChanged(TimerRowInstanceViewModel timerRow)
        {
            DefaultOrbsTimersManager.SetTimerEnabled(timerRow.IsEnabled, timerRow.SourceTimer);
            TimerController.RefreshAvailableTimers();
        }
        private void UpdateRowColors()
        {
            for (var i = 0; i < TimerRows.Count; i++)
            {
                if (i % 2 == 1)
                {
                    TimerRows[i].RowBackground = (SolidColorBrush)App.Current.FindResource("Gray4Brush");
                }
                else
                {
                    TimerRows[i].RowBackground = (SolidColorBrush)App.Current.FindResource("Gray3Brush");
                }
            }
        }
        internal void SetScalar(double sizeScalar)
        {
            _encounterTimersWindow.SetScale(sizeScalar);
            _disciplineTimersWindow.SetScale(sizeScalar);

        }
    }
}
