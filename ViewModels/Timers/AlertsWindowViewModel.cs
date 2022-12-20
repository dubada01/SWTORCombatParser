using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Views.Timers;

namespace SWTORCombatParser.ViewModels.Timers;

public class AlertsWindowViewModel:INotifyPropertyChanged
{
    private ITimerWindow _timerWindow;
    private bool _timersEnabled;
    private List<TimerInstance> _createdTimers = new List<TimerInstance>();
    private bool active;
    private (string, string, string) _currentBossInfo;

    public event Action CloseRequested = delegate { };
    public event Action<bool> OnLocking = delegate { };
    public event Action<string> OnCharacterDetected = delegate { };
    public event PropertyChangedEventHandler PropertyChanged;

    public bool OverlaysMoveable { get; set; }
    private List<TimerInstanceViewModel> _currentTimers = new List<TimerInstanceViewModel>();
    public List<TimerInstanceViewModel> SwtorTimers { get; set; } =
        new List<TimerInstanceViewModel>();

    public bool Active
    {
        get => active;
        set
        {
            active = value;
            if (!active)
            {
                HideTimers();
            }
            else
            {
                if (OverlaysMoveable)
                {
                    _timerWindow.Show();
                }
            }

        }
    }

    public AlertsWindowViewModel()
    {
        CombatLogStreamer.HistoricalLogsFinished += EnableTimers;
        CombatLogStreamer.CombatUpdated += NewInCombatLogs;
        CombatLogStreamer.NewLineStreamed += NewLogInANDOutOfCombat;


        _timerWindow = new AlertView(this);

        CombatIdentifier.NewCombatAvailable += UpdateBossInfo;

        App.Current.Dispatcher.Invoke(() =>
        {
            var defaultTimersInfo = DefaultGlobalOverlays.GetOverlayInfoForType("Alerts");
            active = defaultTimersInfo.Acive;
            _timerWindow.Top = defaultTimersInfo.Position.Y;
            _timerWindow.Left = defaultTimersInfo.Position.X;
            _timerWindow.Width = defaultTimersInfo.WidtHHeight.X;
            _timerWindow.Height = defaultTimersInfo.WidtHHeight.Y;
        });
        RefreshTimers();
    }

    private void UpdateBossInfo(Combat obj)
    {
        if (obj != null && obj.IsCombatWithBoss)
            _currentBossInfo = CombatIdentifier.CurrentCombat.EncounterBossDifficultyParts;
        else
            _currentBossInfo = ("", "", "");
    }

    public void ShowTimers()
    {
        if (!Active)
            return;
        _timerWindow.Show();
    }

    public void HideTimers()
    {
        App.Current.Dispatcher.Invoke(() => { _timerWindow.Hide(); });
    }

    public void RefreshTimers()
    {
        _createdTimers.ForEach(t => t.TimerOfTypeExpired -= RefreshTimerVisuals);
        _createdTimers.ForEach(t => t.NewTimerInstance -= AddTimerVisual);
        var defaultTimersInfo = DefaultTimersManager.GetAllDefaults();
        var timers = defaultTimersInfo.SelectMany(v => v.Timers);
        var validTimers = timers.Where(t => t.IsAlert);
        var timerInstances = validTimers.Select(t => new TimerInstance(t)).ToList();

        timerInstances.ForEach(t => t.TimerOfTypeExpired += RefreshTimerVisuals);
        timerInstances.ForEach(t => t.NewTimerInstance += AddTimerVisual);
        _createdTimers = new List<TimerInstance>(timerInstances);
    }

    private void AddTimerVisual(TimerInstanceViewModel obj)
    {
        if (obj.SourceTimer.IsHot || !Active)
            return;
        App.Current.Dispatcher.Invoke(() =>
        {
            ShowTimers();
            _currentTimers.Add(obj);
            SwtorTimers = new List<TimerInstanceViewModel>(_currentTimers.OrderBy(t => t.TimerValue));
            OnPropertyChanged("SwtorTimers");
        });
    }

    private void NewLogInANDOutOfCombat(ParsedLogEntry log)
    {
        var currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(log.TimeStamp);
        var validTimers =
            _createdTimers.Where(t => t.TrackOutsideOfCombat && CheckEncounterAndBoss(t, currentEncounter));
        Parallel.ForEach(validTimers, timer => { timer.CheckForTrigger(log, DateTime.Now); });
    }

    private bool CheckEncounterAndBoss(TimerInstance t, EncounterInfo encounter)
    {
        var timerEncounter = t.SourceTimer.SpecificEncounter;
        var timerDifficulty = t.SourceTimer.SpecificDifficulty;
        var timerBoss = t.SourceTimer.SpecificBoss;
        if (timerEncounter == "All")
            return true;

        if (encounter.Name == timerEncounter && (encounter.Difficutly == timerDifficulty || timerDifficulty == "All") &&
            _currentBossInfo.Item1 == timerBoss)
            return true;
        return false;
    }

    private void NewInCombatLogs(CombatStatusUpdate obj)
    {        
        if (obj.Type == UpdateType.Start)
        {
            UncancellBeforeCombat();
        }

        if (obj.Type == UpdateType.Stop)
        {
            CancelAfterCombat();
        }

        if (obj.Logs == null || !_timersEnabled || obj.Type == UpdateType.Stop)
            return;
        var currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(obj.CombatStartTime);

        var logs = obj.Logs;
        foreach (var log in logs)
        {
            var validTimers =
                _createdTimers.Where(t => !t.TrackOutsideOfCombat && CheckEncounterAndBoss(t, currentEncounter));
            Parallel.ForEach(validTimers, timer => { timer.CheckForTrigger(log, obj.CombatStartTime); });
        }
    }

    private void UncancellBeforeCombat()
    {
        foreach (var timer in _createdTimers)
        {
            timer.UnCancel();
        }
    }

    private void CancelAfterCombat()
    {
        foreach (var timer in _createdTimers.Where(t => !t.SourceTimer.TrackOutsideOfCombat))
        {
            timer.Cancel();
        }
    }

    private void RefreshTimerVisuals(TimerInstanceViewModel removedTimer)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            _currentTimers.Remove(removedTimer);
            SwtorTimers = new List<TimerInstanceViewModel>(_currentTimers.OrderBy(t => t.TimerValue));
            if(SwtorTimers.Count == 0)
                HideTimers();
            OnPropertyChanged("SwtorTimers");
        });

    }

    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void EnableTimers(DateTime combatEndTime, bool localPlayerIdentified)
    {
        _timersEnabled = true;
    }

    internal void UpdateLock(bool value)
    {
        OverlaysMoveable = !value;
        OnPropertyChanged("OverlaysMoveable");
        OnLocking(value);
    }

    internal void EnabledChangedForTimer(bool isEnabled, string id)
    {
        var timerToUpdate = _createdTimers.FirstOrDefault(t => t.SourceTimer.Id == id);
        if (timerToUpdate == null)
            return;
        timerToUpdate.IsEnabled = isEnabled;
    }
}