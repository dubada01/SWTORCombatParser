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
        private System.Timers.Timer _timer;
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

        public double MaxTimerValue
        {
            get => _maxTimerValue;
            set
            {
                _maxTimerValue = value;
                
                OnPropertyChanged("TimerDuration");
            }
        }

        public double CurrentRatio => TimerValue / MaxTimerValue;
        public Duration TimerDuration { get; set; }
        public Color TimerColor => SourceTimer.TimerColor;
        public SolidColorBrush TimerBackground => new SolidColorBrush(TimerColor);

        public double TimerValue
        {
            get => timerValue; set
            {
                timerValue = value;

                if (MaxTimerValue == 0 || TimerValue < 0 || TimerValue > MaxTimerValue)
                {
                    TimerDuration  = new Duration(TimeSpan.Zero);
                }
                else
                    TimerDuration = new Duration(TimeSpan.FromSeconds(timerValue));
                OnPropertyChanged("TimerDuration");
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


        public TimerInstanceViewModel(Timer swtorTimer)
        {
            _playAtTime = !string.IsNullOrEmpty(swtorTimer.CustomAudioPath) && File.Exists(swtorTimer.CustomAudioPath) ? swtorTimer.AudioStartTime : 3;
            _audioPath = !string.IsNullOrEmpty(swtorTimer.CustomAudioPath) && File.Exists(swtorTimer.CustomAudioPath) ? swtorTimer.CustomAudioPath :
                swtorTimer.IsAlert ? Path.Combine(Environment.CurrentDirectory, "resources/Audio/AlertSound.wav") :
                Path.Combine(Environment.CurrentDirectory, "resources/Audio/3210_Sound.wav");
            SourceTimer = swtorTimer;
            MaxTimerValue = swtorTimer.DurationSec;
            App.Current.Dispatcher.Invoke(() => { _mediaPlayer = new MediaPlayer(); });
            _dtimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            //_timer = new System.Timers.Timer();
            if (!swtorTimer.IsAlert)
            {
                _timerValue = TimeSpan.FromSeconds(MaxTimerValue);
                _dtimer.Interval = TimeSpan.FromMilliseconds(100);
                //_timer.Interval = 100d;
            }
            else
            {
                _timerValue = TimeSpan.FromSeconds(swtorTimer.IsSubTimer ? 0 : 3);
                
                if (SourceTimer.IsSubTimer)
                {
                    return;
                }
                _dtimer.Interval = TimeSpan.FromSeconds(3);
               // _timer.Interval =  3000d;
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
            TimerValue = MaxTimerValue + offset;
            OnPropertyChanged("TimerDuration");
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public void TriggerTimeTimer(DateTime timeStampWhenTrigged)
        {
            Debug.WriteLine(DateTime.Now + ": +++++ Triggered! - " + SourceTimer.Name);
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
                // _timer.Elapsed += Tick;
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
                // _timer.Elapsed += ClearAlert;
            }
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");

            isActive = true;
            _dtimer.IsEnabled = true;
            _dtimer.Start();
            // _timer.Enabled = true;
            // _timer.Start();
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
        private double _maxTimerValue;

        public void Complete(bool endedNatrually)
        {
            lock(completeLock)
            {
                if (!isActive) return;
                isActive = false;
                _dtimer.Stop();
                _dtimer.IsEnabled = false;
                /*_timer.Stop();
                _timer.Enabled = false;*/
                Debug.WriteLine(DateTime.Now + ": ----- Complete! - " + SourceTimer.Name+ " Was cancelled: " + !endedNatrually);
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
            //isActive = false;
            //_dtimer.Stop();
            //_dtimer.IsEnabled = false;
        }
    }
}
