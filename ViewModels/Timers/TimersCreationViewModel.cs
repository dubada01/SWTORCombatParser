using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.Timers.Boss_Timers;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.Timers.HOT_Timers;
using SWTORCombatParser.DataStructures.Timers.Defensive_Timers;
using SWTORCombatParser.DataStructures.Timers.Offensive_Timers;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Encounter_Selection;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.ViewModels.Timers
{
    public enum TimerType
    {
        Encounter,
        Discipline
    }
    public class TimersCreationViewModel : INotifyPropertyChanged
    {
        private ITimerWindowViewModel _disciplineTimersWindow;
        private ITimerWindowViewModel _encounterTimersWindow;
        private AlertsWindowViewModel _alertTimersWindow;
        private EncounterSelectionViewModel _enounterSelectionViewModel;
        private TimerType selectedTimerSourceType = TimerType.Discipline;
        private bool _isLocked;
        private Timer _timerEdited;
        private bool disciplineTimersActive;
        private bool alertTimersActive;

        private List<DefaultTimersData> _savedTimersData = new List<DefaultTimersData>();

        public event PropertyChangedEventHandler PropertyChanged;

        public EncounterSelectionView EncounterSelectionView
        {
            get => _encounterSelectionView;
            set
            {
                if (Equals(value, _encounterSelectionView)) return;
                _encounterSelectionView = value;
                OnPropertyChanged();
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
                _timerSourcesTypes = value;
                OnPropertyChanged();
            }
        }

        private string selectedTimerSource;

        public TimerType SelectedTimerSourceType
        {
            get => selectedTimerSourceType; set
            {
                selectedTimerSourceType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisciplineTimerSelected));
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
                OnPropertyChanged("AvailableTimerSources");
                OnPropertyChanged("DisciplineTimerSelected");
                OnPropertyChanged("SelectedTimerSourceType");
                OnPropertyChanged("TimerActiveCheck");
                OnPropertyChanged("VisibleTimerSelected");
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
                _availableTimerSources = value;
                OnPropertyChanged();
            }
        }

        public string SelectedTimerSource
        {
            get => selectedTimerSource; set
            {
                selectedTimerSource = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VisibleTimerSelected));

                OnPropertyChanged();
                UpdateTimerRows();
                OnPropertyChanged("VisibleTimerSelected");
            }
        }

        public List<string> EncounterTimersList
        {
            get => _encounterTimersList;
            set
            {
                if (Equals(value, _encounterTimersList)) return;
                _encounterTimersList = value;
                OnPropertyChanged();
            }
        }

        public List<string> DisciplineTimersList
        {
            get => _disciplineTimersList;
            set
            {
                if (Equals(value, _disciplineTimersList)) return;
                _disciplineTimersList = value;
                OnPropertyChanged();
            }
        }

        public bool DisciplineTimersActive
        {
            get => disciplineTimersActive; set
            {
                if (value == disciplineTimersActive)
                    return;
                disciplineTimersActive = value;
                OnPropertyChanged();
                if (disciplineTimersActive)
                {
                    _disciplineTimersWindow.Active = true;
                    if (CombatMonitorViewModel.IsLiveParseActive())
                    {
                        _disciplineTimersWindow.SetSource(SelectedTimerSource);
                        _disciplineTimersWindow.ShowTimers(_isLocked);
                    }
                }
                else
                {
                    _disciplineTimersWindow.Active = false;
                }
                DefaultTimersManager.UpdateTimersActive(DisciplineTimersActive, SelectedTimerSource);
                OnPropertyChanged();
            }
        }

        public bool AlertsActive
        {
            get => alertTimersActive;
            set
            {
                if (value == alertTimersActive)
                    return;
                _alertTimersWindow.Active = value;
                alertTimersActive = value;
                DefaultGlobalOverlays.SetActive("Alerts", value);
                OnPropertyChanged();
            }
        }

        public void TryShow()
        {
            if (DisciplineTimersActive)
                _disciplineTimersWindow.ShowTimers(_isLocked);
        }
        public void HideTimers()
        {
            _disciplineTimersWindow.HideTimers();
        }

        public ObservableCollection<TimerRowInstanceViewModel> TimerRows
        {
            get => _timerRows;
            set
            {
                if (Equals(value, _timerRows)) return;
                _timerRows = value;
                OnPropertyChanged();
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
                    OnPropertyChanged("SelectedTimerSource");
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
            _disciplineTimersWindow = new TimersWindowViewModel();
            _alertTimersWindow = new AlertsWindowViewModel();
            _encounterTimersWindow = new EncounterTimerWindowViewModel();
            alertTimersActive = _alertTimersWindow.Active;
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
                DefaultTimersManager.UpdateTimersActive(false, source);
            }
            if (!string.IsNullOrEmpty(source))
            {
                SelectedTimerSource = source;
                _disciplineTimersWindow.SetSource(SelectedTimerSource);
                DisciplineTimersActive = DefaultTimersManager.GetTimersActive(source);
                OnPropertyChanged("DisciplineTimersActive");
            }

        }
        private void RefreshAvaialbleTriggerOwners()
        {
            _savedTimersData = DefaultTimersManager.GetAllDefaults();
            if (_savedTimersData.All(t => t.TimerSource != "Shared"))
            {
                _savedTimersData.Add(new DefaultTimersData() { TimerSource = "Shared" });
                DefaultTimersManager.SetSavedTimers(new List<Timer>(), "Shared");
            }
            var savedTimerSources = new List<string>(_savedTimersData.Where(t => t.TimerSource != null && !t.TimerSource.Contains('|')).Select(t => t.TimerSource));
            savedTimerSources = savedTimerSources.OrderBy(s => s).ToList();
            savedTimerSources.SwapItems(0, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "Shared")));
            savedTimerSources.SwapItems(1, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "HOTS")));
            savedTimerSources.SwapItems(2, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "DOTS")));
            savedTimerSources.SwapItems(3, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "DCD")));
            savedTimerSources.SwapItems(4, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "OCD")));

            DisciplineTimersList = savedTimerSources;
            OnPropertyChanged("DisciplineTimersList");
        }

        public ICommand CreateNewTimerCommand => new CommandHandler(CreateNewTimer);

        private void CreateNewTimer(object obj)
        {
            var vm = new ModifyTimerViewModel(SelectedTimerSource);
            vm.OnNewTimer += NewTimer;
            var t = new TimerModificationWindow(vm);
            t.ShowDialog();
        }
        public string AudioImageSource => !allMuted ? Environment.CurrentDirectory + "/resources/audioIcon.png" : Environment.CurrentDirectory + "/resources/mutedIcon.png";
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

        public bool AllActive
        {
            get => allActive; set
            {
                allActive = value;
                OnPropertyChanged();
                if (TimerRows.Count == 0)
                    return;
                Task.Run(() =>
                {
                    Parallel.ForEach(TimerRows, timer =>
                    {
                        timer.SetActive(allActive);
                    });
                    DefaultTimersManager.SetTimersEnabledForSource(TimerRows.Select(t => t.SourceTimer).ToList(), TimerRows.First().SourceTimer.TimerSource);
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
                _canChangeAudio = value;
                OnPropertyChanged();
            }
        }

        public ICommand ToggleAudioCommand => new CommandHandler(ToggleAudio);

        private void SetAudioIcon(bool status)
        {
            allMuted = status;
            OnPropertyChanged("AudioImageSource");
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
                DefaultTimersManager.SetTimersAudioForSource(TimerRows.Select(t => t.SourceTimer).ToList(), TimerRows.First().SourceTimer.TimerSource);
                TimerController.RefreshAvailableTimers();
            });
        }

        public string ImportId
        {
            get => _importId;
            set
            {
                if (value == _importId) return;
                _importId = value;
                OnPropertyChanged();
            }
        }

        public ICommand ImportCommand => new CommandHandler(Import);

        private async void Import(object obj)
        {
            if (TimerRows.Any(t => t.SourceTimer.ShareId == ImportId))
                return;
            var timer = await TimerDatabaseAccess.GetTimerFromId(ImportId);
            if (timer == null)
                return;
            timer.IsEnabled = true;
            timer.TimerSource = selectedTimerSource;
            NewTimer(timer, false);
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

        private void NewTimer(Timer obj, bool wasEdit)
        {
            if (wasEdit)
                DefaultTimersManager.RemoveTimerForCharacter(_timerEdited, SelectedTimerSource);
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
            DefaultTimersManager.AddTimersForSource(new List<Timer>() { timer }, SelectedTimerSource);

        }
        public void UpdateLock(bool state)
        {
            _disciplineTimersWindow.UpdateLock(state);
            _alertTimersWindow.UpdateLock(state);
            _encounterTimersWindow.UpdateLock(state);
            _isLocked = state;
            if (!_isLocked)
                _alertTimersWindow.ShowTimers();
            else
                _alertTimersWindow.HideTimers();
        }
        public void SetClass(Entity player, SWTORClass swtorclass)
        {
            if (!player.IsLocalPlayer || !CombatMonitorViewModel.IsLiveParseActive() || SelectedTimerSource == swtorclass.Discipline || CombatDetector.InCombat)
                return;
            _disciplineTimersWindow.SetPlayer(swtorclass);
            if (!DisciplineTimersList.Contains(swtorclass.Discipline))
            {
                DisciplineTimersList.Add(swtorclass.Discipline);
            }

            SelectedTimerSourceType = TimerType.Discipline;
            SelectedTimerSource = swtorclass.Discipline;

            _disciplineTimersWindow.SetSource(SelectedTimerSource);
            DisciplineTimersActive = DefaultTimersManager.GetTimersActive(SelectedTimerSource);

            RefreshAvaialbleTriggerOwners();
        }
        private void UpdateTimerRows()
        {
            _savedTimersData = DefaultTimersManager.GetAllDefaults();
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
            OnPropertyChanged("TimerRows");
            OnPropertyChanged("AllActive");
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
            DefaultTimersManager.RemoveTimerForCharacter(obj.SourceTimer, SelectedTimerSource);
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
            var shareWindow = new TimerSharePopup(id);
            shareWindow.ShowDialog();
            DefaultTimersManager.SetIdForTimer(obj.SourceTimer, SelectedTimerSource, id);
            await TimerDatabaseAccess.AddTimer(obj.SourceTimer);
        }
        private void Edit(TimerRowInstanceViewModel obj)
        {
            _timerEdited = obj.SourceTimer;
            TimerRows.Remove(obj);
            var vm = new ModifyTimerViewModel(SelectedTimerSource);
            vm.OnNewTimer += NewTimer;
            vm.OnCancelEdit += CancelEdit;
            var t = new TimerModificationWindow(vm);
            vm.Edit(_timerEdited.Copy());
            t.ShowDialog();
        }
        private void ActiveChanged(TimerRowInstanceViewModel timerRow)
        {
            DefaultTimersManager.SetTimerEnabled(timerRow.IsEnabled, timerRow.SourceTimer);
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
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void SetScalar(double sizeScalar)
        {
            _encounterTimersWindow.SetScale(sizeScalar);
            _disciplineTimersWindow.SetScale(sizeScalar);

        }
    }
}
