using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class EncounterTimerWindowViewModel : TimersWindowViewModel
    {
        private bool inBossRoom;
        private bool isEnabled;
        public EncounterTimerWindowViewModel(string overlayName) : base(overlayName)
        {
            TimerTitle = "Boss Timers";
            SwtorTimers = new List<TimerInstanceViewModel>();

            TimerController.TimerExpired += RemoveTimer;
            TimerController.TimerTriggered += AddTimerVisual;
            TimerController.ReorderRequested += ReorderTimers;
            CombatLogStateBuilder.AreaEntered += AreaEntered;
            CombatLogStreamer.HistoricalLogsFinished += CheckForArea;
            DefaultBossFrameManager.DefaultsUpdated += UpdateState;
            CombatLogStreamer.CombatUpdated += CheckForEnd;
            isEnabled = DefaultBossFrameManager.GetDefaults().PredictMechs;
            _timerWindow = new TimersWindow(this);
            Dispatcher.UIThread.Invoke(() =>
            {
                var defaultTimersInfo = DefaultGlobalOverlays.GetOverlayInfoForType(_overlayName);
                _timerWindow.Position = new PixelPoint((int)defaultTimersInfo.Position.X, (int)defaultTimersInfo.Position.Y);
                _timerWindow.Width = defaultTimersInfo.WidtHHeight.X;
                _timerWindow.Height = defaultTimersInfo.WidtHHeight.Y;
            });
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
                    SwtorTimers = new List<TimerInstanceViewModel>();
                }
            }
        }
        private void UpdateState()
        {
            isEnabled = DefaultBossFrameManager.GetDefaults().PredictMechs;
            if (_active && !isEnabled)
            {
                Active = false;
            }
            if ((inBossRoom || OverlaysMoveable) && isEnabled)
            {
                Active = true;
            }
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

        private object _timerChangeLock = new object();
        private double _currentScale;

        private void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
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
            ReorderTimers();
            callback(obj);
        }

        private void RemoveTimer(TimerInstanceViewModel removedTimer, Action<TimerInstanceViewModel> callback)
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.Remove(removedTimer);
            }
            ReorderTimers();
            callback(removedTimer);
        }

        private void ReorderTimers()
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.RemoveAll(t => t.TimerValue <= 0);
                SwtorTimers = new List<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
            }
        }
    }
}
