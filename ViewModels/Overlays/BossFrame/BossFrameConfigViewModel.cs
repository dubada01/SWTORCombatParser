using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Views.Overlay.BossFrame;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using System.Windows.Input;
using Prism.Commands;
using Microsoft.VisualBasic.Logging;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class BossFrameConfigViewModel : INotifyPropertyChanged
    {
        private bool overlaysMoveable = false;
        private bool bossFrameEnabled;
        private bool dotTrackingEnabled;
        private bool mechPredictionsEnabled;
        private string combatDuration;
        private DispatcherTimer _timer;
        private bool _inCombat;

        public BrossFrameView View { get; set; }
        public bool OverlaysMoveable
        {
            get => overlaysMoveable; set
            {
                overlaysMoveable = value;
                OnPropertyChanged("ShowFrame");
            }
        }
        public bool BossFrameEnabled
        {
            get => bossFrameEnabled; set
            {
                bossFrameEnabled = value;
                if (!bossFrameEnabled)
                    View.Hide();
                if (bossFrameEnabled)
                    View.Show();
                DefaultBossFrameManager.SetActiveState(bossFrameEnabled);
                OnPropertyChanged();
            }
        }
        public bool DotTrackingEnabled
        {
            get => dotTrackingEnabled; set
            {
                dotTrackingEnabled = value;
                DefaultBossFrameManager.SetDotTracking(dotTrackingEnabled);
                UpdateBossFrameStates();
                OnPropertyChanged();
            }
        }
        public bool MechPredictionsEnabled
        {
            get => mechPredictionsEnabled; set
            {
                mechPredictionsEnabled = value;
                DefaultBossFrameManager.SetPredictMechs(mechPredictionsEnabled);
                UpdateBossFrameStates();
                OnPropertyChanged();
            }
        }
        public bool RaidChallengesEnabled
        {
            get => raidChallengesEnabled; set
            {
                raidChallengesEnabled = value;
                DefaultBossFrameManager.SetRaidChallenges(raidChallengesEnabled);
                UpdateBossFrameStates();
                OnPropertyChanged();
            }
        }
        public ICommand IncreaseCommand => new DelegateCommand(Increase);

        private void Increase()
        {
            CurrentScale += 0.1;
        }
        public ICommand DecreaseCommand => new DelegateCommand(Decrease);

        private void Decrease()
        {
            CurrentScale -= 0.1;
        }
        public double CurrentScale
        {
            get => currentScale; set
            {
                currentScale = Math.Round(value, 1);
                DefaultBossFrameManager.SetScale(currentScale);
                UpdateBossFrameScale();
                OnPropertyChanged();
            }
        }
        public bool ShowFrame => BossesDetected.Any() || OverlaysMoveable;
        public event Action<bool> OnLocking = delegate { };
        public ObservableCollection<BossFrameViewModel> BossesDetected { get; set; } = new ObservableCollection<BossFrameViewModel>();
        public string CombatDuration
        {
            get => combatDuration; set
            {
                combatDuration = value;
                OnPropertyChanged();
            }
        }
        private DateTime _lastUpdateTime;
        private double _accurateDuration;
        private double currentScale = 1;
        private bool raidChallengesEnabled;

        public BossFrameConfigViewModel()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (e, r) =>
            {
                _accurateDuration += (DateTime.Now - _lastUpdateTime).TotalSeconds;
                CombatDuration = TimeSpan.FromSeconds(_accurateDuration).ToString(@"mm\:ss");
                _lastUpdateTime = DateTime.Now;
            };

            CombatLogStreamer.CombatUpdated += OnNewLog;
            CombatLogStreamer.NewLineStreamed += HandleNewLog;
            View = new BrossFrameView(this);
            var currentDefaults = DefaultBossFrameManager.GetDefaults();
            CurrentScale = currentDefaults.Scale == 0 ? 1 : currentDefaults.Scale;
            View.Left = currentDefaults.Position.X;
            View.Top = currentDefaults.Position.Y;
            View.Width = currentDefaults.WidtHHeight.X;
            View.MainArea.MinHeight = currentDefaults.WidtHHeight.Y;

            BossFrameEnabled = currentDefaults.Acive;
            DotTrackingEnabled = currentDefaults.TrackDOTS;
            MechPredictionsEnabled = currentDefaults.PredictMechs;
            RaidChallengesEnabled = currentDefaults.RaidChallenges;

            if (currentDefaults.Acive)
                View.Show();
        }



        public void LockOverlays()
        {
            OnLocking(true);
            OverlaysMoveable = false;
            OnPropertyChanged("OverlaysMoveable");
            OnPropertyChanged("ShowFrame");
        }
        public void UnlockOverlays()
        {
            OnLocking(false);
            OverlaysMoveable = true;
            OnPropertyChanged("OverlaysMoveable");
            OnPropertyChanged("ShowFrame");
        }
        private void UpdateBossFrameScale()
        {
            foreach (var boss in BossesDetected)
            {
                boss.UpdateBossFrameScale(CurrentScale);
            }
        }
        private void UpdateBossFrameStates()
        {
            foreach (var boss in BossesDetected)
            {
                boss.UpdateBossFrameState(DotTrackingEnabled, MechPredictionsEnabled);
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
            if(log.Effect.EffectType == EffectType.TargetChanged || !_inCombat)
            {
                return;
            }
            var encounterInfo = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(log.TimeStamp);
            if (encounterInfo.BossNames.Count == 0)
                return;
            var currentEncounterBossTargets = encounterInfo.BossInfos.SelectMany(b => b.TargetIds).ToList();
            if (currentEncounterBossTargets.Contains(log.Source.LogId.ToString()) || currentEncounterBossTargets.Contains(log.Target.LogId.ToString()))
            {
                EntityInfo boss = currentEncounterBossTargets.Contains(log.Source.LogId.ToString()) ? log.SourceInfo : log.TargetInfo;

                if (BossesDetected.All(b => b.CurrentBoss.LogId != boss.Entity.LogId) && boss.CurrentHP > 0)
                {
                    bool isDuplicate = BossesDetected.Any(b => b.CurrentBoss.Name == boss.Entity.Name);
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        BossesDetected.Add(new BossFrameViewModel(boss, DotTrackingEnabled, MechPredictionsEnabled, isDuplicate, CurrentScale));
                        OnPropertyChanged("ShowFrame");
                    });
                }
                else
                {
                    var activeBoss = BossesDetected.FirstOrDefault(b => b.CurrentBoss.LogId == boss.Entity.LogId);
                    if (activeBoss == null)
                        return;
                    if (boss.CurrentHP == 0 || (log.Effect.EffectId == _7_0LogParsing.DeathCombatId && log.Target.LogId == activeBoss.CurrentBoss.LogId))
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            BossesDetected.Remove(activeBoss);
                            OnPropertyChanged("ShowFrame");
                        });

                    }
                    else
                        activeBoss.LogWithBoss(boss);
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
            App.Current.Dispatcher.Invoke(() =>
            {
                BossesDetected.Clear();
                OnPropertyChanged("ShowFrame");
            });
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
