using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using SWTORCombatParser.ViewModels.Overlays.BossFrame;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class EncounterTimerWindowViewModel : TimersWindowViewModel
    {
        private bool inBossRoom;
        public override bool ShouldBeVisible => inBossRoom && Active;
        public EncounterTimerWindowViewModel(string overlayName) : base(overlayName)
        {
            SwtorTimers = new ObservableCollection<TimerInstanceViewModel>();
            BossFrameConfigViewModel.InCombatWithBoss += HandleBossCombatChanged;
            CombatLogStreamer.CombatUpdated += CheckForEnd;
            MainContent = new TimersWindow(this);
        }

        private void HandleBossCombatChanged(bool obj)
        {
            inBossRoom = obj;
            UpdateVisibility();
        }

        private void CheckForEnd(CombatStatusUpdate obj)
        {
            if (obj.Type == UpdateType.Stop)
            {
                lock (_timerChangeLock)
                {
                    foreach (var timer in SwtorTimers)
                    {
                        timer.Dispose();
                    }
                    SwtorTimers = new ObservableCollection<TimerInstanceViewModel>();
                }
            }
        }
        
        private object _timerChangeLock = new object();
        private double _currentScale;

        protected override void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (!obj.SourceTimer.IsMechanic || obj.SourceTimer.IsAlert ||
                obj.SourceTimer.TriggerType == TimerKeyType.EntityHP || obj.SourceTimer.TriggerType == TimerKeyType.AbsorbShield || obj.TimerValue <= 0)
            {
                callback(obj);
                return;
            }
            obj.Scale = _currentScale;
            lock (_timerChangeLock)
            {
                _visibleTimers.Add(obj);
            }
            ReorderTimers("Any");
            callback(obj);
        }

        protected override void RemoveTimer(TimerInstanceViewModel removedTimer, Action<TimerInstanceViewModel> callback)
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.Remove(removedTimer);
            }
            ReorderTimers("Any");
            callback(removedTimer);
        }

        protected override void ReorderTimers(string id)
        {
            lock (_timerChangeLock)
            {                
                if(_visibleTimers.All(t => t.SourceTimer.Id != id) && id != "Any")
                    return;
                _visibleTimers.RemoveAll(t => t.TimerValue <= 0);
                SwtorTimers = new ObservableCollection<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
            }
        }
    }
}
