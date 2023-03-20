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
using System.Windows.Documents;
using System.Windows.Threading;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Timers
{
    public interface ITimerWindowViewModel
    {
        bool OverlaysMoveable { get; set; }
        event Action<bool> OnLocking;
        event Action CloseRequested;
        event Action<string> OnCharacterDetected;
        void HideTimers();
        void ShowTimers(bool locked);
        void SetPlayer(SWTORClass classInfo);
        void UpdateLock(bool locked);
        void SetSource(string source);
        void Closing();
        void SetScale(double sizeScalar);

        bool Active { get; set; }
    }
    public class TimersWindowViewModel : INotifyPropertyChanged, ITimerWindowViewModel
    {
        private string _timerSource;
        private ITimerWindow _timerWindow;
        private bool _timersEnabled;
        private List<TimerInstance> _activeTimers = new List<TimerInstance>();
        private bool active;
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
                if (active != value)
                    DefaultTimersManager.UpdateTimersActive(active, _timerSource);
                active = value;
                if (!active)
                {
                    HideTimers();
                }
                else
                {
                    if (OverlaysMoveable)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            _timerWindow.Show();
                        });
                    } 
                }

            }
        }

        public TimersWindowViewModel()
        {
            TimerController.TimerExpired += RemoveTimer;
            TimerController.TimerTriggered += AddTimerVisual;
            TimerController.ReorderRequested += ReorderTimers;
            _timerWindow = new TimersWindow(this);
            _timerWindow.SetIdText("DISCIPLINE TIMERS");
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
            if (_timerSource.Contains('|') || _timerSource == "Shared" || _timerSource == "HOTS")
                return;
            TimerTitle = _timerSource + " Timers";
            OnPropertyChanged("TimerTitle");
            SwtorTimers = new List<TimerInstanceViewModel>();
            _timerWindow.SetPlayer(_timerSource);
            App.Current.Dispatcher.Invoke(() =>
            {
                var defaultTimersInfo = DefaultTimersManager.GetDefaults(_timerSource);
                _timerWindow.Top = defaultTimersInfo.Position.Y;
                _timerWindow.Left = defaultTimersInfo.Position.X;
                _timerWindow.Width = defaultTimersInfo.WidtHHeight.X;
                _timerWindow.Height = defaultTimersInfo.WidtHHeight.Y;
                ShowTimers(!OverlaysMoveable);
            });
        }
        private object _timerChangeLock = new object();
        private double _currentScale;

        private void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (obj.SourceTimer.IsHot || !Active || obj.SourceTimer.IsMechanic || obj.SourceTimer.IsAlert || obj.SourceTimer.IsBuiltInDefensive)
            {
                callback(obj);
                return;
            }
            lock (_timerChangeLock)
            {
                obj.Scale = _currentScale;
                _visibleTimers.Add(obj);
                SwtorTimers = new List<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
                callback(obj);
            }
            OnPropertyChanged("SwtorTimers");
        }

        private void RemoveTimer(TimerInstanceViewModel removedTimer, Action<TimerInstanceViewModel> callback)
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.Remove(removedTimer);
                SwtorTimers = new List<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
                callback(removedTimer);
            }
            OnPropertyChanged("SwtorTimers");
        }
        private void ReorderTimers()
        {
            SwtorTimers = new List<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
            OnPropertyChanged("SwtorTimers");
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

    }
}
