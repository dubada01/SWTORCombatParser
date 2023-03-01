using SWTORCombatParser.DataStructures;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Timer = SWTORCombatParser.DataStructures.Timer;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimerInstanceViewModel : INotifyPropertyChanged, IDisposable
    {
        private DateTime _lastUpdateTime;
        private double _maxTimerValue =1;
        private bool displayTimerValue;
        private int charges;
        private bool displayTimer;
        private double _hpTimerMonitor = 0;
        private double _absorbRemaining = 0;
        private double _maxAbsorb = 0;
        private double timerValue = 1;
        private int _updateIntervalMs;
        private MediaPlayer _mediaPlayer;
        private string _audioPath;
        private double _playAtTime;
        private bool isActive;
        public event Action<TimerInstanceViewModel,bool> TimerExpired = delegate { };
        public event Action TimerRefreshed = delegate {  };
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

        public double CurrentRatio =>double.IsNaN(TimerValue / MaxTimerValue) ? 1 : (TimerValue / MaxTimerValue);
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
                    TimerDuration  = new Duration(TimeSpan.FromSeconds(MaxTimerValue));
                }
                else
                    TimerDuration = new Duration(TimeSpan.FromSeconds(timerValue));
                OnPropertyChanged("TimerDuration");
                if (SourceTimer.TriggerType != TimerKeyType.AbsorbShield) return;
                OnPropertyChanged("CurrentRatio");
                OnPropertyChanged("TimerName");
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
            if (swtorTimer.UseAudio)
            {
                //builtin-timer-audio
                if (!string.IsNullOrEmpty(swtorTimer.CustomAudioPath) && File.Exists(Path.Combine(Environment.CurrentDirectory, "resources/Audio/TimerAudio/", swtorTimer.CustomAudioPath)))
                {
                    _audioPath = Path.Combine(Environment.CurrentDirectory, "resources/Audio/TimerAudio/",swtorTimer.CustomAudioPath);
                }
                else
                {
                    _audioPath = !string.IsNullOrEmpty(swtorTimer.CustomAudioPath) && File.Exists(swtorTimer.CustomAudioPath) ? swtorTimer.CustomAudioPath :
                        swtorTimer.IsAlert ?  Path.Combine(Environment.CurrentDirectory, "resources/Audio/AlertSound.wav") :
                        Path.Combine(Environment.CurrentDirectory, "resources/Audio/3210_Sound.wav");
                }


                Application.Current.Dispatcher.Invoke(() => { 
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.Open(new Uri(_audioPath, UriKind.RelativeOrAbsolute));
                    if (swtorTimer.AudioStartTime == 0)
                        _playAtTime = 2;
                    else
                        _playAtTime = swtorTimer.AudioStartTime;
                });

            }

            SourceTimer = swtorTimer;
            MaxTimerValue = swtorTimer.DurationSec;
            TimerValue = swtorTimer.DurationSec;
            OnPropertyChanged("CurrentRatio");
            
            if (!swtorTimer.IsAlert)
            {
                _updateIntervalMs = 100;
            }
            else
            {
                _updateIntervalMs = 3000;
            }
        }


        public void Reset(DateTime timeStampOfReset)
        {
            var offset = (DateTime.Now - timeStampOfReset).TotalSeconds * -1;
            StartTime = timeStampOfReset;
            TimerValue = MaxTimerValue + offset;
            TimerRefreshed();
            App.Current.Dispatcher.Invoke(() =>
            {
                if(_mediaPlayer!= null)
                    _mediaPlayer.Stop();
            });
            OnPropertyChanged("TimerDuration");
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public async void TriggerTimeTimer(DateTime timeStampWhenTrigged)
        {
            isActive = true;
            if (!SourceTimer.IsAlert)
            {
                if (SourceTimer.HideUntilSec == 0)
                {
                    DisplayTimer = true;
                }
                DisplayTimerValue = true;
                var offset = (DateTime.Now - timeStampWhenTrigged).TotalSeconds * -1;


                offset = 0;


                TimerValue = MaxTimerValue + offset;
                _lastUpdateTime = DateTime.Now;
                OnPropertyChanged("CurrentRatio");
                OnPropertyChanged("TimerValue");
                while (TimerValue > 0 && isActive)
                {
                    UpdateTimeBasedTimer();
                    await Task.Delay(_updateIntervalMs);
                }
                if(isActive)
                    Complete(true);
            }
            else
            {
                if (SourceTimer.UseAudio)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        _mediaPlayer.Stop();
                        _mediaPlayer.Play();
                    });
                }
                DisplayTimer = true;
                DisplayTimerValue = false;
                if (SourceTimer.TriggerType != TimerKeyType.HasEffect && !SourceTimer.IsSubTimer)
                {
                    await Task.Delay(_updateIntervalMs);
                    Complete(true);
                }
            }

        }
        public async void TriggerHPTimer(double currentHP)
        {
            DisplayTimer = true;
            DisplayTimerValue = true;
            MaxTimerValue = 100d;
            CurrentMonitoredHP = currentHP;
            _hpTimerMonitor = currentHP;
            TimerValue = SourceTimer.HPPercentage;
            OnPropertyChanged("TimerValue");
            isActive = true;
            while (_hpTimerMonitor > SourceTimer.HPPercentage && isActive)
            {
                _hpTimerMonitor = CurrentMonitoredHP;
                await Task.Delay(_updateIntervalMs);
            }
            if(isActive)
                Complete(true);

        }

        public async void TriggerAbsorbTimer(double maxAbsorb)
        {
            DisplayTimer = true;
            DisplayTimerValue = false;
            MaxTimerValue = 1d;
            TimerValue = 1d;
            OnPropertyChanged("TimerValue");
            DamageDoneToAbsorb = 0;
            _absorbRemaining = maxAbsorb;
            _maxAbsorb = maxAbsorb;
            isActive = true;
            while (TimerValue > 0 && isActive)
            {
                _absorbRemaining = _maxAbsorb - DamageDoneToAbsorb;
                TimerValue = _absorbRemaining / _maxAbsorb;
                OnPropertyChanged("TimerValue");
                await Task.Delay(_updateIntervalMs);
            }
            if(isActive)
                Complete(true);
        }

        private void UpdateTimeBasedTimer()
        {
            var deltaTime = (DateTime.Now - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = DateTime.Now;
            TimerValue -= deltaTime;
            OnPropertyChanged("TimerValue");
            if (SourceTimer.UseAudio && TimerValue <= _playAtTime)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _mediaPlayer.Play();
                });
            }
            if (SourceTimer.HideUntilSec > 0 && !DisplayTimer && TimerValue <= SourceTimer.HideUntilSec)
                DisplayTimer = true;
        }
        public void Complete(bool endedNatrually)
        {
            if (!isActive) return;
            isActive = false;
            TimerValue = 0;
            TimerExpired(this, endedNatrually);
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
            TimerValue = 0;
            isActive = false;
        }
    }
}
