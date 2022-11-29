using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimersWindowViewModel : INotifyPropertyChanged
    {
        private string _timerSource;
        private ITimerWindow _timerWindow;
        private bool _timersEnabled;
        private List<TimerInstance> _createdTimers = new List<TimerInstance>();
        private bool active;
        private (string, string, string) _currentBossInfo;
        private bool isAlerts;

        public event Action CloseRequested = delegate { };
        public event Action<bool> OnLocking = delegate { };
        public event Action<string> OnCharacterDetected = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public bool OverlaysMoveable { get; set; } = true;
        public ObservableCollection<TimerInstanceViewModel> SwtorTimers { get; set; } = new ObservableCollection<TimerInstanceViewModel>();

        public string TimerTitle { get; set; }
        public bool Active
        {
            get => active;
            set
            {
                active = value;
                if (!active)
                {
                    HideTimers();
                }
            }
        }

        public TimersWindowViewModel(bool isAlert = false)
        {
            CombatLogStreamer.HistoricalLogsFinished += EnableTimers;
            CombatLogStreamer.CombatUpdated += NewInCombatLogs;
            CombatLogStreamer.NewLineStreamed += NewLogInANDOutOfCombat;
            isAlerts = isAlert;
            if (isAlert)
                _timerWindow = new AlertView(this);
            else
                _timerWindow = new TimersWindow(this);
            CombatIdentifier.NewCombatAvailable += UpdateBossInfo;
            if (isAlerts)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    var defaultTimersInfo = DefaultOverlayManager.GetDefaults("All")["Alerts"];
                    _timerWindow.Top = defaultTimersInfo.Position.Y;
                    _timerWindow.Left = defaultTimersInfo.Position.X;
                    _timerWindow.Width = defaultTimersInfo.WidtHHeight.X;
                    _timerWindow.Height = defaultTimersInfo.WidtHHeight.Y;
                    ShowTimers(!OverlaysMoveable);
                });
            }
        }

        private void UpdateBossInfo(Combat obj)
        {
            if (obj != null && obj.IsCombatWithBoss)
                _currentBossInfo = CombatIdentifier.CurrentCombat.EncounterBossDifficultyParts;
            else
                _currentBossInfo = ("", "", "");
        }

        public void ShowTimers(bool isLocked)
        {
            if (!Active)
                return;
            App.Current.Dispatcher.Invoke(() =>
            {
                _timerWindow.Show();
                UpdateLock(isLocked);
            });

        }
        public void HideTimers()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                _timerWindow.Hide();
            });
        }
        public string GetSource()
        {
            return _timerSource;
        }
        public void SetSource(string sourceName)
        {
            if (_timerSource == sourceName)
                return;
            _timerSource = sourceName;
            UpdateSource();
        }
        public void SetPlayer(SWTORClass swtorclass)
        {
            if (_timerSource == swtorclass.Discipline)
                return;
            _timerSource = swtorclass.Discipline;
            UpdateSource();
        }
        private void UpdateSource()
        {
            TimerTitle = _timerSource + " Timers";
            OnPropertyChanged("TimerTitle");
            SwtorTimers = new ObservableCollection<TimerInstanceViewModel>();
            _timerWindow.SetPlayer(_timerSource);
            RefreshTimers();
            App.Current.Dispatcher.Invoke(() =>
            {
                var defaultTimersInfo = DefaultTimersManager.GetDefaults(_timerSource);
                _timerWindow.Top = defaultTimersInfo.Position.Y;
                _timerWindow.Left = defaultTimersInfo.Position.X;
                _timerWindow.Width = defaultTimersInfo.WidtHHeight.X;
                _timerWindow.Height = defaultTimersInfo.WidtHHeight.Y;
                if (_timerSource.Contains('|') || _timerSource == "Shared" || _timerSource == "HOTS")
                    return;
                ShowTimers(!OverlaysMoveable);
            });
        }
        public void RefreshTimers()
        {
            if (string.IsNullOrEmpty(_timerSource))
                return;
            _createdTimers.ForEach(t => t.TimerOfTypeExpired -= RefreshTimerVisuals);
            _createdTimers.ForEach(t => t.NewTimerInstance -= AddTimerVisual);
            var defaultTimersInfo = DefaultTimersManager.GetAllDefaults();
            var timers = new List<Timer>();
            if (!_timerSource.Contains('|'))
            {
                timers = defaultTimersInfo.SelectMany(d => d.Timers).ToList();
            }
            else
            {
                timers = DefaultTimersManager.GetDefaults(_timerSource).Timers;
            }
            var validTimers = isAlerts ? timers.Where(t => t.IsAlert) : timers.Where(t => !t.IsAlert);
            var timerInstances = timers.Select(t => new TimerInstance(t)).ToList();
            foreach (var timerInstance in timerInstances)
            {
                if (!string.IsNullOrEmpty(timerInstance.ExperiationTimerId))
                {
                    var trigger = timerInstances.First(t => t.SourceTimer.Id == timerInstance.ExperiationTimerId);
                    timerInstance.ExpirationTimer = trigger;
                }
            }
            timerInstances.ForEach(t => t.TimerOfTypeExpired += RefreshTimerVisuals);
            timerInstances.ForEach(t => t.NewTimerInstance += AddTimerVisual);
            _createdTimers = new List<TimerInstance>(timerInstances);
        }

        private void AddTimerVisual(TimerInstanceViewModel obj)
        {
            if (obj.SourceTimer.IsHot)
                return;
            App.Current.Dispatcher.Invoke(() =>
            {
                SwtorTimers.Add(obj);
                SwtorTimers = new ObservableCollection<TimerInstanceViewModel>(SwtorTimers.OrderBy(t => t.TimerValue));
                OnPropertyChanged("SwtorTimers");
            });
        }

        private void NewLogInANDOutOfCombat(ParsedLogEntry log)
        {
            var currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(log.TimeStamp);
            var validTimers = _createdTimers.Where(t => t.TrackOutsideOfCombat && CheckEncounterAndBoss(t, currentEncounter));
            Parallel.ForEach(validTimers, timer =>
            {
                timer.CheckForTrigger(log, DateTime.Now);
            });
        }

        private bool CheckEncounterAndBoss(TimerInstance t, EncounterInfo encounter)
        {
            var timerEncounter = t.SourceTimer.SpecificEncounter;
            var timerDifficulty = t.SourceTimer.SpecificDifficulty;
            var timerBoss = t.SourceTimer.SpecificBoss;
            if (timerEncounter == "All")
                return true;

            if (encounter.Name == timerEncounter && (encounter.Difficutly == timerDifficulty || timerDifficulty == "All") && _currentBossInfo.Item1 == timerBoss)
                return true;
            return false;
        }

        private void NewInCombatLogs(CombatStatusUpdate obj)
        {
            if (obj.Type == UpdateType.Start)
            {
                UncancellBeforeCombat();
            }
            if (obj.Type == UpdateType.Stop)
            {
                CancelAfterCombat();
            }
            if (obj.Logs == null || !_timersEnabled || obj.Type == UpdateType.Stop)
                return;
            var currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(obj.CombatStartTime);

            var logs = obj.Logs;
            foreach (var log in logs)
            {
                var validTimers = _createdTimers.Where(t => !t.TrackOutsideOfCombat && CheckEncounterAndBoss(t, currentEncounter));
                Parallel.ForEach(validTimers, timer =>
                {
                    timer.CheckForTrigger(log, obj.CombatStartTime);
                });
            }
        }
        private void UncancellBeforeCombat()
        {
            foreach (var timer in _createdTimers)
            {
                timer.UnCancel();
            }
        }
        private void CancelAfterCombat()
        {
            foreach (var timer in _createdTimers.Where(t=>!t.SourceTimer.TrackOutsideOfCombat))
            {
                timer.Cancel();
            }
        }
        private void RefreshTimerVisuals(TimerInstanceViewModel removedTimer)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                SwtorTimers.Remove(removedTimer);
                SwtorTimers = new ObservableCollection<TimerInstanceViewModel>(SwtorTimers.OrderBy(t => t.TimerValue));
                OnPropertyChanged("SwtorTimers");
            });

        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private void EnableTimers(DateTime combatEndTime)
        {
            _timersEnabled = true;
        }
        internal void UpdateLock(bool value)
        {
            OverlaysMoveable = !value;
            OnPropertyChanged("OverlaysMoveable");
            OnLocking(value);
        }

        internal void EnabledChangedForTimer(bool isEnabled, string id)
        {
            var timerToUpdate = _createdTimers.FirstOrDefault(t => t.SourceTimer.Id == id);
            if (timerToUpdate == null)
                return;
            timerToUpdate.IsEnabled = isEnabled;
        }
    }
}
