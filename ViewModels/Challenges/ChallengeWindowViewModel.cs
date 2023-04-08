using Microsoft.VisualBasic.Logging;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeWindowViewModel : INotifyPropertyChanged
    {
        private bool overlaysMoveable;
        private bool active;
        private bool isEnabled;
        private ChallengeWindow _challengeWindow;
        private ChallengeUpdater _challengeUpdater;
        private bool inBossRoom;

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action CloseRequested = delegate { };
        public event Action<bool> OnLocking = delegate { };
        public ObservableCollection<ChallengeInstanceViewModel> ActiveChallengeInstances { get; set; } = new ObservableCollection<ChallengeInstanceViewModel>();
        public bool OverlaysMoveable
        {
            get => overlaysMoveable; set
            {
                overlaysMoveable = value;
                OnPropertyChanged();
            }
        }

        public void RefreshChallenges()
        {
            _challengeUpdater.RefreshChallenges();
        }
        public ChallengeWindowViewModel()
        {
            _challengeUpdater = new ChallengeUpdater();
            _challengeUpdater.SetCollection(ActiveChallengeInstances);
            CombatLogStateBuilder.AreaEntered += AreaEntered;
            CombatLogStreamer.HistoricalLogsFinished += CheckForArea;
            DefaultBossFrameManager.DefaultsUpdated += UpdateState;

            isEnabled = DefaultBossFrameManager.GetDefaults().RaidChallenges;
            _challengeWindow = new ChallengeWindow(this);
            App.Current.Dispatcher.Invoke(() =>
            {
                var defaultTimersInfo = DefaultGlobalOverlays.GetOverlayInfoForType("Challenge"); ;
                _challengeWindow.Top = defaultTimersInfo.Position.Y;
                _challengeWindow.Left = defaultTimersInfo.Position.X;
                _challengeWindow.Width = defaultTimersInfo.WidtHHeight.X;
                _challengeWindow.Height = defaultTimersInfo.WidtHHeight.Y;
            });
        }


        private void CheckForArea(DateTime arg1, bool arg2)
        {
            var currentArea = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(TimeUtility.CorrectedTime);
            if (currentArea.IsBossEncounter)
            {
                if (isEnabled)
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

        private void AreaEntered(EncounterInfo areaInfo)
        {
            if (areaInfo.IsBossEncounter)
            {
                if (isEnabled)
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
        private void UpdateState()
        {
            isEnabled = DefaultBossFrameManager.GetDefaults().RaidChallenges;
            if (active && !isEnabled)
            {
                Active = false;
            }
            if ((inBossRoom || OverlaysMoveable) && isEnabled)
            {
                Active = true;
            }
        }

        internal void OverlayClosing()
        {
            Active = false;
            DefaultBossFrameManager.SetRaidChallenges(Active);
        }
        public bool Active
        {
            get => active;
            set
            {
                active = value;
                if (!active)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        _challengeWindow.Hide();
                    });
                }
                else
                {

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        _challengeWindow.Show();
                    });

                }

            }
        }
        internal void UpdateLock(bool value)
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
            OnLocking(value);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void SetScale(double sizeScalar)
        {
            _challengeUpdater.UpdateScale(sizeScalar);
            App.Current.Dispatcher.Invoke(() => { 
                foreach(var challenge in ActiveChallengeInstances)
                {
                    challenge.Scale = sizeScalar;
                }
            });
        }
    }
}
