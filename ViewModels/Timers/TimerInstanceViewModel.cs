using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using ManagedBass;
using ReactiveUI;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimerInstanceViewModel : ReactiveObject, IDisposable
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
        private bool _audioLoaded;
        private MediaPlayer _mediaPlayer;
        private LibVLC _libvlc;
        private string _audioPath;
        private double _playAtTime;
        private bool isActive;
        private double scale = 1;
        private double barHeight;
        private double defaultBarHeight = 30;
        private bool _stubTimer;
        private bool _hasAudioPlayed = false;
        private readonly DispatcherTimer _tickTimer;
        public event Action<TimerInstanceViewModel, bool> TimerExpired = delegate { };
        public event Action TimerRefreshed = delegate { };
        public event Action TimerStarted = delegate { };

        public void FireTimerStarted()
        {
            TimerStarted();
        }
        public event Action<int> ChargesUpdated = delegate { };
        public int Charges
        {
            get => charges; set
            {
                this.RaiseAndSetIfChanged(ref charges, value);
                this.RaisePropertyChanged(nameof(ShowCharges));
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
                this.RaisePropertyChanged(nameof(TimerDuration));
            }
        }
        public double OverlayOpacity { get; set; }
        public double CurrentRatio => double.IsNaN(TimerValue / MaxTimerValue) ? 0 : (TimerValue / MaxTimerValue);
        public TimeSpan TimerDuration { get; set; }
        public Color TimerColor => SourceTimer.TimerColor;
        public SolidColorBrush TimerForeground => new SolidColorBrush(TimerColor);
        public bool _isAboutToExpire = false;

        private static SolidColorBrush _defaultTimerBackground = Dispatcher.UIThread.Invoke(() => { return new SolidColorBrush(Colors.WhiteSmoke);}); 
        private static SolidColorBrush _aboutToExpireBackground = Dispatcher.UIThread.Invoke(() => { return new SolidColorBrush(Colors.OrangeRed);}); 
        public SolidColorBrush TimerBackground { get; set; } = _defaultTimerBackground;

        
        //TODO add this to settings config so that it is loaded each time a timer is created
        public bool ShowIcon { get; set; }
        private Bitmap? _infoIcon;
        private readonly int stream;

        public Bitmap? InfoIcon
        {
            get => _infoIcon;
            private set => this.RaiseAndSetIfChanged(ref _infoIcon, value);
        }
        public async Task LoadInfoIconAsync()
        {
            var result = async () =>
            {
                if (!string.IsNullOrEmpty(SourceTimer.Effect) && ulong.TryParse(SourceTimer.Effect, out var parsedEffect) && IconGetter.HasIcon(parsedEffect))
                {
                    return await IconGetter.GetIconForId(parsedEffect);
                }

                if (!string.IsNullOrEmpty(SourceTimer.Ability) && ulong.TryParse(SourceTimer.Ability, out var parsedAbility) &&IconGetter.HasIcon(parsedAbility))
                {
                    return await IconGetter.GetIconForId(parsedAbility);
                }
                return null;
            };
            InfoIcon = await result();
        }
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
                this.RaisePropertyChanged(nameof(TimerDuration));
                if (SourceTimer.TriggerType != TimerKeyType.AbsorbShield) return;
                this.RaisePropertyChanged(nameof(CurrentRatio));
                this.RaisePropertyChanged(nameof(TimerName));
            }
        }
        public bool DisplayTimer
        {
            get => displayTimer; set
            {
                this.RaiseAndSetIfChanged(ref displayTimer, value);
            }
        }
        public bool DisplayTimerValue
        {
            get => displayTimerValue; set
            {
                this.RaiseAndSetIfChanged(ref displayTimerValue, value);
            }
        }

        public double Scale
        {
            get => scale; set
            {
                if (value == 0)
                    return;
                this.RaiseAndSetIfChanged(ref scale, value);
                this.RaisePropertyChanged(nameof(BarHeight));
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
                try
                {
                    //builtin-timer-audio
                    if (!string.IsNullOrEmpty(swtorTimer.CustomAudioPath) && File.Exists(
                            Path.Combine(Environment.CurrentDirectory, "resources/Audio/TimerAudio/",
                                swtorTimer.CustomAudioPath)))
                    {
                        _audioPath = Path.Combine(Environment.CurrentDirectory, "resources/Audio/TimerAudio/",
                            swtorTimer.CustomAudioPath);
                    }
                    else
                    {
                        _audioPath =
                            !string.IsNullOrEmpty(swtorTimer.CustomAudioPath) &&
                            File.Exists(swtorTimer.CustomAudioPath) ? swtorTimer.CustomAudioPath :
                            swtorTimer.IsAlert ? Path.Combine(Environment.CurrentDirectory,
                                "resources/Audio/AlertSound.wav") :
                            Path.Combine(Environment.CurrentDirectory, "resources/Audio/3210_Sound.wav");
                    }
#if WINDOWS
                    _libvlc = new LibVLC();
                    var media = new Media(_libvlc, new Uri(_audioPath, UriKind.RelativeOrAbsolute));
                    _mediaPlayer = new MediaPlayer(media);
#endif
#if MACOS
                    stream = Bass.CreateStream(_audioPath, 0, 0, BassFlags.Default);
#endif

                    if (swtorTimer.AudioStartTime == 0)
                        _playAtTime = 2;
                    else
                        _playAtTime = swtorTimer.AudioStartTime;
                    _audioLoaded = true;
                }
                catch (Exception ex)
                {
                    Logging.LogError("Failed to open audio file for timer at: "+_audioPath);
                }

            }

            SourceTimer = swtorTimer;
            MaxTimerValue = swtorTimer.DurationSec;
            TimerValue = swtorTimer.DurationSec;
            this.RaisePropertyChanged(nameof(CurrentRatio));

            _updateIntervalMs = !swtorTimer.IsAlert ? 100 : 3000;
            
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await LoadInfoIconAsync();
            });
            if (SourceTimer.TriggerType == TimerKeyType.AbsorbShield ||
                SourceTimer.TriggerType == TimerKeyType.EntityHP)
                return;
            _tickTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(_updateIntervalMs),
                DispatcherPriority.Normal,
                (s, e) => OnTick()
            );
        }
        private void OnTick()
        {
            // Early exit if someone stopped the timer:
            if (!isActive)
            {
                _tickTimer.Stop();
                return;
            }

            // 1) Do the same work you had in UpdateTimeBasedTimer()
            UpdateTimeBasedTimer();

            // 2) If we’ve run out, stop & complete:
            if (TimerValue <= 0)
            {
                _tickTimer.Stop();
                Complete(true);
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
            this.RaisePropertyChanged(nameof(TimerDuration));
            this.RaisePropertyChanged(nameof(TimerValue));
        }
        // 3) Replace your old async-loop with this:
        public void TriggerTimeTimer(DateTime timeStampWhenTriggered)
        {
            if (!SourceTimer.IsAlert)
            {
                if (SourceTimer.HideUntilSec == 0)
                    DisplayTimer = true;

                DisplayTimerValue = true;

                var offset = (TimeUtility.CorrectedTime - timeStampWhenTriggered).TotalSeconds * -1;
                TimerValue    = MaxTimerValue + offset;
                _lastUpdateTime = TimeUtility.CorrectedTime;

                this.RaisePropertyChanged(nameof(CurrentRatio));
                this.RaisePropertyChanged(nameof(TimerValue));

                isActive    = true;
                TimerStarted();

                // start the tick loop
                _tickTimer.Interval = TimeSpan.FromMilliseconds(_updateIntervalMs);
                _tickTimer.Start();
            }
            else
            {
                // (unchanged) alert‐sound branch
                if (SourceTimer.UseAudio && _audioLoaded)
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
#if WINDOWS
                        _mediaPlayer.Play();
#endif
#if MACOS
                        Bass.ChannelPlay(stream, false);
#endif
                    });
                }

                DisplayTimer      = true;
                DisplayTimerValue = false;
                isActive          = true;

                // single‐shot completion after one interval
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await Task.Delay(_updateIntervalMs);
                    await Complete(true);
                });
            }
        }
        public async Task TriggerHPTimer(double currentHP)
        {
            DisplayTimer = true;
            DisplayTimerValue = true;
            MaxTimerValue = 100d;
            CurrentMonitoredHP = currentHP;
            _hpTimerMonitor = currentHP;
            TimerValue = SourceTimer.HPPercentage;
            this.RaisePropertyChanged(nameof(TimerValue));
            isActive = true;
            while (_hpTimerMonitor > SourceTimer.HPPercentage && isActive)
            {
                _hpTimerMonitor = CurrentMonitoredHP;
                await Task.Delay(_updateIntervalMs);
            }
            if (isActive)
                await Complete(true);

        }

        public async Task TriggerAbsorbTimer(double maxAbsorb)
        {
            DisplayTimer = true;
            DisplayTimerValue = false;
            MaxTimerValue = 1d;
            TimerValue = 1d;
            this.RaisePropertyChanged(nameof(TimerValue));
            DamageDoneToAbsorb = 0;
            _absorbRemaining = maxAbsorb;
            _maxAbsorb = maxAbsorb;
            isActive = true;
            while (TimerValue > 0 && isActive)
            {
                _absorbRemaining = _maxAbsorb - DamageDoneToAbsorb;
                TimerValue = _absorbRemaining / _maxAbsorb;
                this.RaisePropertyChanged(nameof(TimerValue));
                await Task.Delay(_updateIntervalMs);
            }
            if (isActive)
                await Complete(true);
        }



        private void UpdateTimeBasedTimer()
        {
            var deltaTime = (TimeUtility.CorrectedTime - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = TimeUtility.CorrectedTime;
            TimerValue -= deltaTime;
            this.RaisePropertyChanged(nameof(TimerValue));
            if (_hasAudioPlayed && SourceTimer.UseAudio && _audioLoaded && TimerValue > _playAtTime)
                _hasAudioPlayed = false;
            if (SourceTimer.UseAudio && _audioLoaded && TimerValue <= _playAtTime && !_hasAudioPlayed)
            {
                _hasAudioPlayed = true;
                Dispatcher.UIThread.Invoke(() =>
                {
#if WINDOWS
                        _mediaPlayer.Play();
#endif
#if MACOS
                    Bass.ChannelPlay(stream,false);
#endif
                });
            }
            if(SourceTimer.ChangeBackgroundNearExpiration && TimerValue <= 5 && !_isAboutToExpire)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    _isAboutToExpire = true;
                    TimerBackground = _aboutToExpireBackground;
                    this.RaisePropertyChanged(nameof(TimerBackground));
                });
            }
            if (SourceTimer.ChangeBackgroundNearExpiration && TimerValue > 5 && _isAboutToExpire)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    _isAboutToExpire = false;
                    TimerBackground = _defaultTimerBackground;
                    this.RaisePropertyChanged(nameof(TimerBackground));
                });
            }
            if (SourceTimer.HideUntilSec > 0 && !DisplayTimer && TimerValue <= SourceTimer.HideUntilSec)
                DisplayTimer = true;
        }
        public async Task Complete(bool endedNatrually, bool force = false)
        {
            if (!isActive) return;
            if (SourceTimer.IsHot && !force)
            {
                await DelayRemoval(endedNatrually);
                return;
            }
            isActive = false;
            TimerValue = 0;
            TimerBackground = _defaultTimerBackground;
            TimerExpired(this, endedNatrually);
        }
        private async Task DelayRemoval(bool endedNatrually)
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
        public void Dispose()
        {
            isActive = false;
            _tickTimer.Stop();
            TimerValue = 0;
        }
    }
}
