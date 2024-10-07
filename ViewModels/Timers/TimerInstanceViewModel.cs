using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimerInstanceViewModel : INotifyPropertyChanged, IDisposable
    {
        private DateTime _lastUpdateTime;
        private double _maxTimerValue = 1;
        private bool displayTimerValue;
        private int charges;
        private bool displayTimer;
        private double _hpTimerMonitor = 0;
        private double _absorbRemaining = 0;
        private double _maxAbsorb = 0;
        private double timerValue = 1;
        private int _updateIntervalMs;
        private MediaPlayer _mediaPlayer;
        private LibVLC _libvlc;
        private string _audioPath;
        private double _playAtTime;
        private bool isActive;
        private double scale = 1;
        private double barHeight;
        private double defaultBarHeight = 30;
        private bool _stubTimer;

        public event Action<TimerInstanceViewModel, bool> TimerExpired = delegate { };
        public event Action TimerRefreshed = delegate { };
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
        public bool IsTriggered => isActive;
        public double BarHeight => defaultBarHeight * scale;
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
        public double OverlayOpacity { get; set; }
        public double CurrentRatio => double.IsNaN(TimerValue / MaxTimerValue) ? 1 : (TimerValue / MaxTimerValue);
        public TimeSpan TimerDuration { get; set; }
        public Color TimerColor => SourceTimer.TimerColor;
        public SolidColorBrush TimerForeground => new SolidColorBrush(TimerColor);
        public bool _isAboutToExpire = false;

        private static SolidColorBrush _defaultTimerBackground = Dispatcher.UIThread.Invoke(() => { return new SolidColorBrush(Colors.WhiteSmoke);}); 
        private static SolidColorBrush _aboutToExpireBackground = Dispatcher.UIThread.Invoke(() => { return new SolidColorBrush(Colors.OrangeRed);}); 
        public SolidColorBrush TimerBackground { get; set; } = _defaultTimerBackground;
        public double TimerValue
        {
            get => timerValue; set
            {
                timerValue = value;

                if (MaxTimerValue == 0 || TimerValue < 0 || TimerValue > MaxTimerValue)
                {
                    TimerDuration = TimeSpan.FromSeconds(MaxTimerValue);
                }
                else
                    TimerDuration = TimeSpan.FromSeconds(timerValue);
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

        public double Scale
        {
            get => scale; set
            {
                scale = value;
                OnPropertyChanged("BarHeight");
                OnPropertyChanged();
            }
        }

        public TimerInstanceViewModel(Timer swtorTimer)
        {
            _stubTimer = Settings.ReadSettingOfType<bool>("stub_logs");
            if (swtorTimer.IsCooldownTimer)
            {
                OverlayOpacity = 0.33;
            }
            else
            {
                OverlayOpacity = 1;
            }
            if (swtorTimer.UseAudio)
            {
                //builtin-timer-audio
                if (!string.IsNullOrEmpty(swtorTimer.CustomAudioPath) && File.Exists(Path.Combine(Environment.CurrentDirectory, "resources/Audio/TimerAudio/", swtorTimer.CustomAudioPath)))
                {
                    _audioPath = Path.Combine(Environment.CurrentDirectory, "resources/Audio/TimerAudio/", swtorTimer.CustomAudioPath);
                }
                else
                {
                    _audioPath = !string.IsNullOrEmpty(swtorTimer.CustomAudioPath) && File.Exists(swtorTimer.CustomAudioPath) ? swtorTimer.CustomAudioPath :
                        swtorTimer.IsAlert ? Path.Combine(Environment.CurrentDirectory, "resources/Audio/AlertSound.wav") :
                        Path.Combine(Environment.CurrentDirectory, "resources/Audio/3210_Sound.wav");
                }
                Dispatcher.UIThread.Invoke(() =>
                {
                    _libvlc = new LibVLC();
                    var media = new Media(_libvlc, new Uri(_audioPath, UriKind.RelativeOrAbsolute));
                    _mediaPlayer = new MediaPlayer(media);
                });

                if (swtorTimer.AudioStartTime == 0)
                    _playAtTime = 2;
                else
                    _playAtTime = swtorTimer.AudioStartTime;

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
            if (TimerValue <= 0)
            {
                TriggerTimeTimer(timeStampOfReset);
                TimerRefreshed();
                return;
            }

            var offset = (TimeUtility.CorrectedTime - timeStampOfReset).TotalSeconds * -1;
            StartTime = timeStampOfReset;
            TimerValue = MaxTimerValue + offset;

            TimerRefreshed();
            Dispatcher.UIThread.Invoke(() =>
            {
                if (_mediaPlayer != null)
                {

                    _mediaPlayer.Stop();

                }
            });

            OnPropertyChanged("TimerDuration");
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public async void TriggerTimeTimer(DateTime timeStampWhenTrigged)
        {

            if (!SourceTimer.IsAlert)
            {
                if (SourceTimer.HideUntilSec == 0)
                {
                    DisplayTimer = true;
                }
                DisplayTimerValue = true;
                var offset = (TimeUtility.CorrectedTime - timeStampWhenTrigged).TotalSeconds * -1;

                TimerValue = MaxTimerValue + offset;
                _lastUpdateTime = TimeUtility.CorrectedTime;
                OnPropertyChanged("CurrentRatio");
                OnPropertyChanged("TimerValue");
                isActive = true;
                while (TimerValue > 0 && isActive)
                {
                    UpdateTimeBasedTimer();
                    await Task.Delay(_updateIntervalMs);
                }
                if (isActive)
                    Complete(true);
            }
            else
            {
                if (SourceTimer.UseAudio)
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        _mediaPlayer.Play();
                    });

                }
                DisplayTimer = true;
                DisplayTimerValue = false;
                isActive = true;
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
            if (isActive)
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
            if (isActive)
                Complete(true);
        }

        private void UpdateTimeBasedTimer()
        {
            var deltaTime = (TimeUtility.CorrectedTime - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = TimeUtility.CorrectedTime;
            TimerValue -= deltaTime;
            OnPropertyChanged("TimerValue");
            if (SourceTimer.UseAudio && TimerValue <= _playAtTime)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    _mediaPlayer.Play();
                });
            }
            if(SourceTimer.ChangeBackgroundNearExpiration && TimerValue <= 5 && !_isAboutToExpire)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    _isAboutToExpire = true;
                    TimerBackground = _aboutToExpireBackground;
                    OnPropertyChanged("TimerBackground");
                });
            }
            if (SourceTimer.ChangeBackgroundNearExpiration && TimerValue > 5 && _isAboutToExpire)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    _isAboutToExpire = false;
                    TimerBackground = _defaultTimerBackground;
                    OnPropertyChanged("TimerBackground");
                });
            }
            if (SourceTimer.HideUntilSec > 0 && !DisplayTimer && TimerValue <= SourceTimer.HideUntilSec)
                DisplayTimer = true;
        }
        public void Complete(bool endedNatrually, bool force = false)
        {
            if (!isActive) return;
            if (SourceTimer.IsHot && !force)
            {
                DelayRemoval(endedNatrually);
                return;
            }
            isActive = false;
            TimerValue = 0;
            TimerBackground = _defaultTimerBackground;
            TimerExpired(this, endedNatrually);
        }
        private async void DelayRemoval(bool endedNatrually)
        {
            await Task.Delay(1500);
            if (isActive)
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
            if (SourceTimer.TriggerType != TimerKeyType.AbsorbShield && SourceTimer.TriggerType != TimerKeyType.EntityHP)
            {
                if (SourceTimer.IsAlert)
                {
                    name = !string.IsNullOrEmpty(SourceTimer.AlertText) ? SourceTimer.AlertText : SourceTimer.Name;
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
