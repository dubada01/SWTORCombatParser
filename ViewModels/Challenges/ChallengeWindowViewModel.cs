﻿using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Challenges;
using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Threading;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeWindowViewModel : BaseOverlayViewModel
    {
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
        public ChallengeWindowViewModel(string overlayName):base(overlayName)
        {
            _challengeUpdater = new ChallengeUpdater();
            _challengeUpdater.SetCollection(ActiveChallengeInstances);
            CombatLogStateBuilder.AreaEntered += AreaEntered;
            CombatLogStreamer.HistoricalLogsFinished += CheckForArea;
            DefaultBossFrameManager.DefaultsUpdated += UpdateState;

            Active = DefaultBossFrameManager.GetDefaults().RaidChallenges;
            MainContent = new ChallengeWindow(this);
        }
        
        private void CheckForArea(DateTime arg1, bool arg2)
        {
            var currentArea = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(TimeUtility.CorrectedTime);
            if (currentArea.IsBossEncounter)
            {
                if (Active)
                    ShouldBeVisible = true;
                inBossRoom = true;
            }
            else
            {
                if (!OverlaysMoveable)
                    ShouldBeVisible = false;
                inBossRoom = false;
            }
        }

        private void AreaEntered(EncounterInfo areaInfo)
        {
            if (areaInfo.IsBossEncounter)
            {
                if (Active)
                    ShouldBeVisible = true;
                inBossRoom = true;
            }
            else
            {
                if (!OverlaysMoveable)
                    ShouldBeVisible = false;
                inBossRoom = false;
            }
        }
        private void UpdateState()
        {
            Active = DefaultBossFrameManager.GetDefaults().RaidChallenges;
            if ((inBossRoom || OverlaysMoveable) && Active)
            {
                ShouldBeVisible = true;
            }
            else
            {
                ShouldBeVisible = false;
            }
        }

        internal void UpdateLock(bool value)
        {
            OverlaysMoveable = !value;
            if (OverlaysMoveable && Active)
            {
                ShouldBeVisible = true;
            }
            else
            {
                if (!inBossRoom || !Active)
                {
                    ShouldBeVisible = false;
                }
            }
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
