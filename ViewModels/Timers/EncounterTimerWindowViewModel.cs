using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class EncounterTimerWindowViewModel:INotifyPropertyChanged, ITimerWindowViewModel
    {
        private ITimerWindow _timerWindow;
        private bool active;
        public event Action CloseRequested = delegate { };
        public event Action<bool> OnLocking = delegate { };
        public event Action<string> OnCharacterDetected = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public bool OverlaysMoveable { get; set; }
        public List<TimerInstanceViewModel> SwtorTimers { get; set; } = new List<TimerInstanceViewModel>();
        public List<TimerInstanceViewModel> _visibleTimers = new List<TimerInstanceViewModel>();
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
                else
                {

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        _timerWindow.Show();
                    });

                }

            }
        }

        public EncounterTimerWindowViewModel()
        {
            TimerTitle = "Boss Timers";
            OnPropertyChanged("TimerTitle");
            SwtorTimers = new List<TimerInstanceViewModel>();

            TimerController.TimerExpired += RemoveTimer;
            TimerController.TimerTiggered += AddTimerVisual;
            TimerController.ReorderRequested += ReorderTimers;
            CombatLogStateBuilder.AreaEntered += AreaEntered;
            CombatLogStreamer.HistoricalLogsFinished += CheckForArea;
            _timerWindow = new TimersWindow(this);
            _timerWindow.SetIdText("BOSS TIMERS");
            _timerWindow.SetPlayer("Encounter");
            App.Current.Dispatcher.Invoke(() =>
            {
                var defaultTimersInfo = DefaultGlobalOverlays.GetOverlayInfoForType("Encounter"); ;
                _timerWindow.Top = defaultTimersInfo.Position.Y;
                _timerWindow.Left = defaultTimersInfo.Position.X;
                _timerWindow.Width = defaultTimersInfo.WidtHHeight.X;
                _timerWindow.Height = defaultTimersInfo.WidtHHeight.Y;
            });
        }

        private void CheckForArea(DateTime arg1, bool arg2)
        {
            var currentArea = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(DateTime.Now);
            if (currentArea.IsBossEncounter)
            {
                Active = true;
            }
            else
                Active = false;
        }

        private void AreaEntered(EncounterInfo areaInfo)
        {
            if (areaInfo.IsBossEncounter)
            {
                Active = true;
            }
            else
                Active = false;
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
        private object _timerChangeLock = new object();
        private void AddTimerVisual(TimerInstanceViewModel obj)
        {
            if (!obj.SourceTimer.IsMechanic || obj.SourceTimer.IsAlert || obj.SourceTimer.TriggerType == TimerKeyType.EntityHP)
                return;
            Debug.WriteLine(DateTime.Now+": Attempting to add visual for "+obj.SourceTimer.Name);
            lock (_timerChangeLock)
            {
                _visibleTimers.Add(obj);
                SwtorTimers = new List<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
                OnPropertyChanged("SwtorTimers");
                Debug.WriteLine(DateTime.Now+": Added visual for "+obj.SourceTimer.Name);
            }
        }

        private void RemoveTimer(TimerInstanceViewModel removedTimer)
        {
            Debug.WriteLine(DateTime.Now+": Attempting removal");
            lock (_timerChangeLock)
            {
                _visibleTimers.Remove(removedTimer);
                SwtorTimers = new List<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
                OnPropertyChanged("SwtorTimers");
                Debug.WriteLine(DateTime.Now+": timer removed: "+removedTimer.TimerName);
            }
            Debug.WriteLine(DateTime.Now+": Unlocked after removal!");

        }

        private void ReorderTimers()
        {
            lock (_timerChangeLock)
            {
                SwtorTimers = new List<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
                OnPropertyChanged("SwtorTimers");
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void UpdateLock(bool value)
        {
            OverlaysMoveable = !value;
            OnPropertyChanged("OverlaysMoveable");
            OnLocking(value);
        }

        public void SetPlayer(SWTORClass classInfo)
        {
            
        }

        public void SetSource(string source)
        {
            
        }
    }
}
