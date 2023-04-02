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
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Model.CombatParsing;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class EncounterTimerWindowViewModel:INotifyPropertyChanged, ITimerWindowViewModel
    {
        private ITimerWindow _timerWindow;
        private bool active;
        private bool inBossRoom;
        private bool isEnabled;
        public event Action CloseRequested = delegate { };
        public event Action<bool> OnLocking = delegate { };
        public event Action<string> OnCharacterDetected = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public bool OverlaysMoveable { get; set; }
        public List<TimerInstanceViewModel> SwtorTimers { get; set; } = new List<TimerInstanceViewModel>();
        public List<TimerInstanceViewModel> _visibleTimers = new List<TimerInstanceViewModel>();
        public string TimerTitle { get; set; }

        public void Closing()
        {
            Active = false;
        }
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
            TimerController.TimerTriggered += AddTimerVisual;
            TimerController.ReorderRequested += ReorderTimers;
            CombatLogStateBuilder.AreaEntered += AreaEntered;
            CombatLogStreamer.HistoricalLogsFinished += CheckForArea;
            DefaultBossFrameManager.DefaultsUpdated += UpdateState;
            CombatLogStreamer.CombatUpdated += CheckForEnd;
            isEnabled = DefaultBossFrameManager.GetDefaults().PredictMechs;
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

        private void CheckForEnd(CombatStatusUpdate obj)
        {
            if(obj.Type == UpdateType.Stop)
            {
                lock (_timerChangeLock)
                {
                    foreach (var timer in SwtorTimers)
                    {
                        timer.Dispose();
                    }
                    SwtorTimers = new List<TimerInstanceViewModel>();
                    OnPropertyChanged("SwtorTimers");
                }
            }
        }

        public void SetScale(double scale)
        {
            _currentScale = scale;
            App.Current.Dispatcher?.Invoke(() =>
            {
                foreach (var timer in SwtorTimers)
                {
                    timer.Scale = scale;
                }
            });
        }
        private void UpdateState()
        {
            isEnabled = DefaultBossFrameManager.GetDefaults().PredictMechs;
            if(active && !isEnabled)
            {
                Active = false;
            }
            if((inBossRoom || OverlaysMoveable) && isEnabled)
            {
                Active = true;
            }
        }

        private void CheckForArea(DateTime arg1, bool arg2)
        {
            var currentArea = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(DateTime.Now);
            if (currentArea.IsBossEncounter)
            {
                if(isEnabled)
                    Active = true;
                inBossRoom = true;
            }
            else
            { 
                if(!OverlaysMoveable)
                    Active = false;
                inBossRoom = false;
            }
        }

        private void AreaEntered(EncounterInfo areaInfo)
        {
            if (areaInfo.IsBossEncounter)
            {
                if(isEnabled)
                    Active = true;
                inBossRoom = true;
            }
            else
            {
                if (!OverlaysMoveable)
                    Active = false;
                inBossRoom = false;
            }
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
        private double _currentScale;

        private void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (!obj.SourceTimer.IsMechanic || obj.SourceTimer.IsAlert ||
                obj.SourceTimer.TriggerType == TimerKeyType.EntityHP || obj.SourceTimer.TriggerType == TimerKeyType.AbsorbShield || obj.TimerValue <= 0)
            {
                callback(obj);
                return;
            }
            obj.Scale = _currentScale;
            lock (_timerChangeLock)
            {
                _visibleTimers.Add(obj);
            }
            ReorderTimers();
            callback(obj);
        }

        private void RemoveTimer(TimerInstanceViewModel removedTimer, Action<TimerInstanceViewModel> callback)
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.Remove(removedTimer);
            }
            ReorderTimers();
            callback(removedTimer);
        }

        private void ReorderTimers()
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.RemoveAll(t => t.TimerValue <= 0);
                SwtorTimers = new List<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
            }
            OnPropertyChanged("SwtorTimers");
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void UpdateLock(bool value)
        {
            OverlaysMoveable = !value;
            if (OverlaysMoveable)
            {
                Active = true;
            }
            else
            {
                if (!inBossRoom || !isEnabled)
                {
                    Active = false;
                }
            }
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
