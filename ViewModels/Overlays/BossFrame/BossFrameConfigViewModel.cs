using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Views.Overlay.BossFrame;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class BossFrameConfigViewModel : INotifyPropertyChanged
    {
        private bool overlaysMoveable = true;

        public BrossFrameView View { get; set; }
        public bool OverlaysMoveable
        {
            get => overlaysMoveable; set
            {
                overlaysMoveable = value;
                OnPropertyChanged("ShowFrame");
            }
        }
        public bool ShowFrame => BossesDetected.Any() || OverlaysMoveable;
        public event Action CloseRequested = delegate { };
        public event Action<bool> OnLocking = delegate { };
        public event Action<BossFrameConfigViewModel> OverlayClosed = delegate { };
        public ObservableCollection<BossFrameViewModel> BossesDetected { get; set; } = new ObservableCollection<BossFrameViewModel>();

        public BossFrameConfigViewModel()
        {
            CombatLogStreamer.CombatUpdated += OnNewLog;
            View = new BrossFrameView(this);
            var currentDefaults = DefaultBossFrameManager.GetDefaults();

            View.Left = currentDefaults.Position.X;
            View.Top = currentDefaults.Position.Y;
            View.Width = currentDefaults.WidtHHeight.X;
            View.MainArea.MinHeight = currentDefaults.WidtHHeight.Y;

            View.Show();
        }
        public void LockOverlays()
        {
            OnLocking(true);
            OverlaysMoveable = false;
            OnPropertyChanged("OverlaysMoveable");
        }
        public void UnlockOverlays()
        {
            OnLocking(false);
            OverlaysMoveable = true;
            OnPropertyChanged("OverlaysMoveable");
        }
        public void OverlayClosing()
        {
            OverlayClosed(this);
        }

        public void OnNewLog(CombatStatusUpdate update)
        {
            if (update.Type == UpdateType.Stop)
            {
                HideFrames();
            }
            if (update.Logs == null || update.Type == UpdateType.Stop)
                return;
            var logs = update.Logs;
            var currentEncounterBossTargets = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(update.Logs.Last().TimeStamp).BossInfos.SelectMany(bi => bi.TargetNames).ToList();
            foreach (var log in logs)
            {
                if (currentEncounterBossTargets.Contains(log.Source.Name) || currentEncounterBossTargets.Contains(log.Target.Name))
                {
                    EntityInfo boss = currentEncounterBossTargets.Contains(log.Source.Name) ? log.SourceInfo : log.TargetInfo;
                    if (!BossesDetected.Any(b => b.CurrentBoss.Name == boss.Entity.Name))
                    {
                        App.Current.Dispatcher.Invoke(() => {
                            BossesDetected.Add(new BossFrameViewModel(boss));
                            OnPropertyChanged("ShowFrame");
                        });
                    }
                    else
                    {
                        var activeBoss = BossesDetected.First(b => b.CurrentBoss.Name == boss.Entity.Name);
                        activeBoss.LogWithBoss(boss);
                    }
                }
            }
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
