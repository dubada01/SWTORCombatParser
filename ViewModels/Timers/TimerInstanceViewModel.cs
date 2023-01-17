using SWTORCombatParser.DataStructures;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimerInstanceViewModel : INotifyPropertyChanged, IDisposable
    {
        private DispatcherTimer _dtimer;
        private TimeSpan _timerValue;
        private bool displayTimerValue;
        private int charges;
        private bool displayTimer;
        private double _hpTimerMonitor = 0;
        private double _absorbRemaining = 0;
        private double _maxAbsorb = 0;
        private double timerValue;
        private MediaPlayer _mediaPlayer;
        private string _audioPath;
        private int _playAtTime;
        private bool isActive;
        public event Action<TimerInstanceViewModel,bool> TimerExpired = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<int> ChargesUpdated = delegate { };
        public int Charges
        {
            get => charges; set
            {
                charges = value;
                OnPropertyChanged();
                OnPropertyChanged("ShowCharges");
                ChargesUpdated(Charges);
            }
        }
        public Guid TimerId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool ShowCharges => Charges > 1;
        public Timer SourceTimer { get; set; } = new Timer();
        public double CurrentMonitoredHP { get; set; }
        public double DamageDoneToAbsorb { get; set; }
        public string TargetAddendem { get; set; }
        public long TargetId { get; set; }
        public string TimerName => GetTimerName();
        public double MaxTimerValue { get; set; }
        public Color TimerColor => SourceTimer.TimerColor;
        public SolidColorBrush TimerBackground => new SolidColorBrush(TimerColor);

        public double TimerValue
        {
            get => timerValue; set
            {
                timerValue = value;

                if (MaxTimerValue == 0 || TimerValue < 0 || TimerValue > MaxTimerValue)
                {
                    OnPropertyChanged("RemainderWidth");
                    OnPropertyChanged("BarWidth");
                    return; 
                }
                RemainderWidth = GetRemainderWidth();
                BarWidth = GetBarWidth();
                OnPropertyChanged("RemainderWidth");
                OnPropertyChanged("BarWidth");
            }
        }
        public bool DisplayTimer
        {
            get => displayTimer; set
            {
                displayTimer = value;
                OnPropertyChanged();
            }
        }
        public bool DisplayTimerValue
        {
            get => displayTimerValue; set
            {
                displayTimerValue = value;
                OnPropertyChanged();
            }
        }


        public GridLength RemainderWidth { get; set; } = new GridLength(0, GridUnitType.Star);

        private GridLength GetRemainderWidth()
        {
            
            return (SourceTimer.IsAlert ? new GridLength(0, GridUnitType.Star) : new GridLength(1 - (TimerValue / MaxTimerValue), GridUnitType.Star));
        }

        public GridLength BarWidth { get; set; } = new GridLength(1, GridUnitType.Star);

        private GridLength GetBarWidth()
        {
            return (SourceTimer.IsAlert ? new GridLength(1, GridUnitType.Star) : new GridLength(TimerValue / MaxTimerValue, GridUnitType.Star));
        }
        public TimerInstanceViewModel(Timer swtorTimer)
        {
            _playAtTime = !string.IsNullOrEmpty(swtorTimer.CustomAudioPath) && File.Exists(swtorTimer.CustomAudioPath) ? swtorTimer.AudioStartTime : 3;
            _audioPath = !string.IsNullOrEmpty(swtorTimer.CustomAudioPath) && File.Exists(swtorTimer.CustomAudioPath) ? swtorTimer.CustomAudioPath :
                swtorTimer.IsAlert ? Path.Combine(Environment.CurrentDirectory, "resources/Audio/AlertSound.wav") :
                Path.Combine(Environment.CurrentDirectory, "resources/Audio/3210_Sound.wav");
            SourceTimer = swtorTimer;
            MaxTimerValue = swtorTimer.DurationSec;
            App.Current.Dispatcher.Invoke(() => { _mediaPlayer = new MediaPlayer(); });
            _dtimer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher);
            if (!swtorTimer.IsAlert)
            {
                _timerValue = TimeSpan.FromSeconds(MaxTimerValue);
                _dtimer.Interval = TimeSpan.FromMilliseconds(100);
            }
            else
            {
                _timerValue = TimeSpan.FromSeconds(swtorTimer.IsSubTimer ? 0 : 3);
                _dtimer.Interval = TimeSpan.FromSeconds(swtorTimer.IsSubTimer ? 0 : 3);
            }
            MaxTimerValue = _timerValue.TotalSeconds;
            TimerValue = _timerValue.TotalSeconds;
        }


        private void ClearAlert(object sender, EventArgs args)
        {
            Complete(true);
        }
        public void Reset(DateTime timeStampOfReset)
        {
            var offset = (DateTime.Now - timeStampOfReset).TotalSeconds * -1;
            StartTime = timeStampOfReset;
            _timerValue = TimeSpan.FromSeconds(MaxTimerValue + offset);
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public void TriggerTimeTimer(DateTime timeStampWhenTrigged)
        {
            Debug.WriteLine("+++++ Triggered! - " + SourceTimer.Name);
            if (!SourceTimer.IsAlert)
            {
                if (SourceTimer.HideUntilSec == 0)
                {
                    DisplayTimer = true;
                }
                DisplayTimerValue = true;
                var offset = (DateTime.Now - timeStampWhenTrigged).TotalSeconds * -1;
                _timerValue = TimeSpan.FromSeconds(MaxTimerValue + offset);
                TimerValue = _timerValue.TotalSeconds;
                _dtimer.Tick += Tick;
            }
            else
            {
                if (SourceTimer.UseAudio)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        _mediaPlayer.Open(new Uri(_audioPath
                            ,
                            UriKind.RelativeOrAbsolute));
                        _mediaPlayer.Play();
                    });
                }
                DisplayTimer = true;
                DisplayTimerValue = false;
                if (SourceTimer.TriggerType != TimerKeyType.HasEffect &&
                        !SourceTimer.IsSubTimer)
                    _dtimer.Tick += ClearAlert;
            }
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");

            isActive = true;
            _dtimer.IsEnabled = true;
            _dtimer.Start();
            StartTime = DateTime.Now;
            LastUpdate = StartTime;
        }
        public void TriggerHPTimer(double currentHP)
        {
            DisplayTimer = true;
            DisplayTimerValue = true;
            MaxTimerValue = 100d;
            CurrentMonitoredHP = currentHP;
            _hpTimerMonitor = currentHP;
            TimerValue = SourceTimer.HPPercentage;
            _dtimer.Tick += UpdateHP;
            _dtimer.Start();
            isActive = true;
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }

        public void TriggerAbsorbTimer(double maxAbsorb)
        {
            DisplayTimer = true;
            DisplayTimerValue = false;
            MaxTimerValue = 1d;
            TimerValue = 1d;
            DamageDoneToAbsorb = 0;
            _absorbRemaining = maxAbsorb;
            _maxAbsorb = maxAbsorb;
            _dtimer.Tick += UpdateAbsorb;
            _dtimer.Start();
            isActive = true;
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public void Tick(object sender, EventArgs args)
        {
            _timerValue = _timerValue.Add(-1*(DateTime.Now - LastUpdate));
            TimerValue = _timerValue.TotalSeconds;
            if (TimerValue <= _playAtTime
                && timerValue > (_playAtTime - 0.2) && SourceTimer.UseAudio)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    _mediaPlayer.Open(new Uri(_audioPath,UriKind.RelativeOrAbsolute));
                    _mediaPlayer.Play();
                });
            }
            if (SourceTimer.HideUntilSec > 0 && !DisplayTimer && TimerValue <= SourceTimer.HideUntilSec)
                DisplayTimer = true;

            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
            LastUpdate = DateTime.Now;
            if (TimerValue <= 0)
                Complete(true);
        }
        public void UpdateHP(object sender, EventArgs args)
        {
            _hpTimerMonitor = CurrentMonitoredHP;
            if (_hpTimerMonitor <= SourceTimer.HPPercentage)
                Complete(true);
        }

        public void UpdateAbsorb(object sender, EventArgs args)
        {
            _absorbRemaining = _maxAbsorb - DamageDoneToAbsorb;
            TimerValue = _absorbRemaining / _maxAbsorb;
            if(TimerValue <= 0)
                Complete(true);
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("TimerName");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        private object completeLock = new object();
        public void Complete(bool endedNatrually)
        {
            lock(completeLock)
            {
                if (!isActive) return;
                isActive = false;
                _dtimer.Stop();
                _dtimer.IsEnabled = false;
                Debug.WriteLine("----- Complete! - " + SourceTimer.Name+ " Was cancelled: " + !endedNatrually);
                TimerExpired(this, endedNatrually);
            }
        }
        private string GetTimerName()
        {
            var name = "";
            if (SourceTimer.TriggerType == TimerKeyType.EntityHP)
            {
                name = SourceTimer.Name + ": " + SourceTimer.HPPercentage.ToString("N2") + "%";
            }

            if (SourceTimer.TriggerType == TimerKeyType.AbsorbShield)
            {
                name = $"{SourceTimer.Name}: ({_absorbRemaining:n0}/{_maxAbsorb:n0})";
            }
            if(SourceTimer.TriggerType != TimerKeyType.AbsorbShield && SourceTimer.TriggerType != TimerKeyType.EntityHP)
            {
                if (SourceTimer.IsAlert)
                {
                    name =  !string.IsNullOrEmpty(SourceTimer.AlertText) ? SourceTimer.AlertText : SourceTimer.Name;
                }
                else
                {
                    name = SourceTimer.Name + (SourceTimer.ShowTargetOnTimerUI ? ((string.IsNullOrEmpty(TargetAddendem) ? "" : " on ") + TargetAddendem) : "");
                }
            }

            return name;
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void Dispose()
        {
            isActive = false;
            _dtimer.Stop();
            _dtimer.IsEnabled = false;
        }
    }
}
