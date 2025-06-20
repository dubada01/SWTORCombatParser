using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Views.Overlay.BossFrame;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class BossFrameConfigViewModel : BaseOverlayViewModel
    {
        private bool bossFrameEnabled;
        private string combatDuration;
        private System.Timers.Timer _timer;
        private bool _inCombat;
        public override bool ShouldBeVisible => ShowFrame;
        public BrossFrameView _bossFrame { get; set; }
        public static event Action<bool> InCombatWithBoss = delegate { };
        public bool BossFrameEnabled
        {
            get => bossFrameEnabled; set
            {
                Active = value;
                this.RaiseAndSetIfChanged(ref bossFrameEnabled, value);
                if (!bossFrameEnabled)
                    HideOverlayWindow();
                if (bossFrameEnabled)
                    ShowOverlayWindow();
                DefaultBossFrameManager.SetActiveState(bossFrameEnabled);
            }
        }
        public ReactiveCommand<Unit,Unit> IncreaseCommand => ReactiveCommand.Create(Increase);

        private void Increase()
        {
            CurrentScale += 0.1;
        }
        public ReactiveCommand<Unit,Unit> DecreaseCommand => ReactiveCommand.Create(Decrease);

        private void Decrease()
        {
            CurrentScale -= 0.1;
        }
        public double CurrentScale
        {
            get => currentScale; set
            {
                this.RaiseAndSetIfChanged(ref currentScale, Math.Round(value, 1));
                DefaultBossFrameManager.SetScale(currentScale);
                UpdateBossFrameScale();
            }
        }
        public bool ShowFrame => BossesDetected.Any() || OverlaysMoveable;
        public ObservableCollection<BossFrameViewModel> BossesDetected { get; set; } = new ObservableCollection<BossFrameViewModel>();
        public string CombatDuration
        {
            get => combatDuration; set => this.RaiseAndSetIfChanged(ref combatDuration, value);
        }
        private DateTime _lastUpdateTime;
        private double _accurateDuration;
        private double currentScale = 1;

        public BossFrameConfigViewModel(string overlayName) : base(overlayName)
        {
            _timer = new System.Timers.Timer();
            _timer.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
            _timer.Elapsed += (e, r) =>
            {
                _accurateDuration += (DateTime.Now - _lastUpdateTime).TotalSeconds;
                CombatDuration = TimeSpan.FromSeconds(_accurateDuration).ToString(@"mm\:ss");
                _lastUpdateTime = DateTime.Now;
            };
            this.CloseRequested += () =>
            {
                BossFrameEnabled = false;
            };
            CombatLogStreamer.CombatUpdated += OnNewLog;
            CombatLogStreamer.NewLineStreamed += HandleNewLog;
            _bossFrame = new BrossFrameView(this);
            MainContent = _bossFrame;
            SetAutoScaleHeight();
            var currentDefaults = DefaultBossFrameManager.GetDefaults();
            CurrentScale = currentDefaults.Scale == 0 ? 1 : currentDefaults.Scale;
            var bossFrameDefaults = DefaultGlobalOverlays.GetOverlayInfoForType("BossFrame");
            bossFrameEnabled = bossFrameDefaults.Acive;
            this.WhenAnyValue(x => x.OverlaysMoveable).Subscribe(_ => this.RaisePropertyChanged(nameof(ShowFrame)));
            if (currentDefaults.Acive)
                ShowOverlayWindow();
        }



        public void LockOverlays()
        {
            OverlaysMoveable = false;
        }
        public void UnlockOverlays()
        {
            OverlaysMoveable = true;
        }
        private void UpdateBossFrameScale()
        {
            foreach (var boss in BossesDetected)
            {
                boss.UpdateBossFrameScale(CurrentScale);
            }
        }
        public void OnNewLog(CombatStatusUpdate update)
        {
            if (update.Type == UpdateType.Start)
            {
                StartTimer(update.CombatStartTime);
                _inCombat = true;
            }
            if (update.Type == UpdateType.Stop)
            {
                _inCombat = false;
                HideFrames();
                StopTimer();
            }
        }
        private void HandleNewLog(ParsedLogEntry log)
        {
            if (log.Effect.EffectType == EffectType.TargetChanged || !_inCombat)
            {
                return;
            }
            var encounterInfo = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(log.TimeStamp);
            if (encounterInfo.BossNames.Count == 0)
                return;
            var currentEncounterBossTargets = encounterInfo.BossInfos.SelectMany(b => b.TargetIds).ToList();
            if (currentEncounterBossTargets.Contains(log.Source.LogId) || currentEncounterBossTargets.Contains(log.Target.LogId))
            {
                EntityInfo boss = currentEncounterBossTargets.Contains(log.Source.LogId) ? log.SourceInfo : log.TargetInfo;

                if (BossesDetected.All(b => b.CurrentBoss.LogId != boss.Entity.LogId) && boss.CurrentHP > 0)
                {
                    bool isDuplicate = BossesDetected.Any(b => b.CurrentBoss.Name == boss.Entity.Name);
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        BossesDetected.Add(new BossFrameViewModel(boss, isDuplicate, CurrentScale));
                        this.RaisePropertyChanged(nameof(ShowFrame));
                        InCombatWithBoss.InvokeSafely(true);
                        UpdateVisibility();
                    });
                }
                else
                {
                    var activeBoss = BossesDetected.FirstOrDefault(b => b.CurrentBoss.LogId == boss.Entity.LogId);
                    if (activeBoss == null)
                        return;
                    if (boss.CurrentHP == 0 || (log.Effect.EffectId == _7_0LogParsing.DeathCombatId && log.Target.LogId == activeBoss.CurrentBoss.LogId))
                    {
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            BossesDetected.Remove(activeBoss);
                            this.RaisePropertyChanged(nameof(ShowFrame));
                            InCombatWithBoss.InvokeSafely(BossesDetected.Count > 0);
                            UpdateVisibility();
                        });

                    }
                    else
                        activeBoss.LogWithBoss(boss,log.TimeStamp);
                }
            }
        }
        private void StopTimer()
        {
            _timer.Stop();
            CombatDuration = "0:00";
            _accurateDuration = 0;
        }

        private void StartTimer(DateTime startTime)
        {
            _lastUpdateTime = startTime;
            _accurateDuration = 0;
            CombatDuration = "0:00";
            _timer.Start();
        }

        private void HideFrames()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                BossesDetected.Clear();
                InCombatWithBoss.InvokeSafely(false);
                this.RaisePropertyChanged(nameof(ShowFrame));
                UpdateVisibility();
            });
        }
    }
}
