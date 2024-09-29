using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Challenges;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Threading;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeWindowViewModel : BaseOverlayViewModel
    {
        private bool isEnabled;
        private ChallengeWindow _challengeWindow;
        private ChallengeUpdater _challengeUpdater;
        private bool inBossRoom;
        
        public ObservableCollection<ChallengeInstanceViewModel> ActiveChallengeInstances { get; set; } = new ObservableCollection<ChallengeInstanceViewModel>();

        public void RefreshChallenges()
        {
            _challengeUpdater.RefreshChallenges();
        }
        public void CombatSelected(Combat combatSelected)
        {
            _challengeUpdater.CombatSelected(combatSelected);
        }
        public void CombatUpdated(Combat combat)
        {
            _challengeUpdater.UpdateCombats(combat);
        }
        public ChallengeWindowViewModel()
        {
            _challengeUpdater = new ChallengeUpdater();
            _challengeUpdater.SetCollection(ActiveChallengeInstances);
            CombatLogStateBuilder.AreaEntered += AreaEntered;
            CombatLogStreamer.HistoricalLogsFinished += CheckForArea;
            CombatLogStreamer.NewLineStreamed += CheckForConversation;
            DefaultBossFrameManager.DefaultsUpdated += UpdateState;

            isEnabled = DefaultBossFrameManager.GetDefaults().RaidChallenges;
            _challengeWindow = new ChallengeWindow(this);
            Dispatcher.UIThread.Invoke(() =>
            {
                var defaultTimersInfo = DefaultGlobalOverlays.GetOverlayInfoForType("Challenge"); ;
                _challengeWindow.SetSizeAndLocation(new Point(defaultTimersInfo.Position.X, defaultTimersInfo.Position.Y), new Point(defaultTimersInfo.WidtHHeight.X, defaultTimersInfo.WidtHHeight.Y));
            });
        }

        private void CheckForConversation(ParsedLogEntry entry)
        {
            Dispatcher.UIThread.Invoke(() => {
                if (entry.Effect.EffectId == _7_0LogParsing.InConversationEffectId && entry.Effect.EffectType == EffectType.Apply && entry.Source.IsLocalPlayer)
                {
                    _challengeWindow.Hide();
                }
                if (entry.Effect.EffectId == _7_0LogParsing.InConversationEffectId && entry.Effect.EffectType == EffectType.Remove && entry.Source.IsLocalPlayer)
                {
                    if (Active && inBossRoom)
                    {
                        _challengeWindow.Show();
                    }
                }
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
            if (_active && !isEnabled)
            {
                Active = false;
            }
            if ((inBossRoom || OverlaysMoveable) && isEnabled)
            {
                Active = true;
            }
        }

        internal new void UpdateLock(bool value)
        {
            OverlaysMoveable = !value;
            if (OverlaysMoveable && isEnabled)
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
            SetLock(value);
        }
        
        internal void SetScale(double sizeScalar)
        {
            _challengeUpdater.UpdateScale(sizeScalar);
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var challenge in ActiveChallengeInstances)
                {
                    challenge.Scale = sizeScalar;
                }
            });
        }
    }
}
