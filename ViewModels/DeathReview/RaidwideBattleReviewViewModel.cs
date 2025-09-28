using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.Death_Review;

namespace SWTORCombatParser.ViewModels.Death_Review;

public class RaidwideBattleReviewViewModel : ReactiveObject
{
    private UserControl _currentReviewContent;
    private TenSecondRecapView _recapView;
    private TenSecondRecapViewModel _recapViewModel;
    private DamageTakenView _damageTakenView;
    private DamageTakenViewModel _damageTakenViewModel;
    private int _selectedTabIndex;
    private readonly DeathReviewViewModel _legacyDeathReviewVM;
    private readonly DeathReviewPage _legacyDeathReviewView;

    public RaidwideBattleReviewViewModel()
    {
        _recapViewModel = new TenSecondRecapViewModel();
        _recapView = new TenSecondRecapView(_recapViewModel);
        
        _damageTakenViewModel = new DamageTakenViewModel();
        _damageTakenView = new DamageTakenView(_damageTakenViewModel);

        _legacyDeathReviewVM = new DeathReviewViewModel();
        _legacyDeathReviewView = new DeathReviewPage(_legacyDeathReviewVM);
        
        CurrentReviewContent = _damageTakenView;
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            _selectedTabIndex = value;
            if (_selectedTabIndex == 2)
                CurrentReviewContent = _legacyDeathReviewView;
            if(_selectedTabIndex == 1)
                CurrentReviewContent = _recapView;
            if(_selectedTabIndex == 0)
                CurrentReviewContent = _damageTakenView;
        }
    }

    public UserControl CurrentReviewContent
    {
        get => _currentReviewContent;
        set => this.RaiseAndSetIfChanged(ref _currentReviewContent, value);
    }

    public string EncounterName => CombatInstance?.BossInfo?.EncounterName;
    public string CompletionText => CombatInstance != null && CombatInstance.AllLogs.Count > 0 ? CombatInstance.WasBossKilled ? 
        "Cleared in " + TimeSpan.FromSeconds(CombatInstance.DurationSeconds).ToString(@"mm\:ss") + " started by " + CombatInstance.Initiator?.Name : 
        "Wipe at "+ PercentComplete.ToString("N2") + "% at " + TimeSpan.FromSeconds(CombatInstance.DurationSeconds).ToString(@"mm\:ss") + " started by " + CombatInstance.Initiator?.Name : "";
    public double PercentComplete => GetEncounterPercentComplete();
    public Combat CombatInstance { get; set; }

    public void SetCombat(Combat combat)
    {
        CombatInstance = combat;
        _recapViewModel.SetCombat(combat);
        _damageTakenViewModel.SetCombat(combat);
        _legacyDeathReviewVM.AddCombat(combat);
        this.RaisePropertyChanged(nameof(CompletionText));
        this.RaisePropertyChanged(nameof(EncounterName));
    }

    public void Reset()
    {
        CombatInstance = new Combat();
        this.RaisePropertyChanged(nameof(CompletionText));
        this.RaisePropertyChanged(nameof(EncounterName));
    }
    
    private double GetEncounterPercentComplete()
    {
        if(CombatInstance == null)
            return 0;
        var bossNames = CombatInstance.ParentEncounter.BossNames;
        var totalBossHP = 0d;
        var currentBossHP = 0d;

        // Group logs for relevant bosses by boss name
        var logsByBoss = CombatInstance.AllLogs.Values
            .Where(log => bossNames.Contains(log.Source.Name) || bossNames.Contains(log.Target.Name))
            .GroupBy(log => bossNames.FirstOrDefault(boss => boss == log.Source.Name || boss == log.Target.Name));

        // Process each boss
        foreach (var boss in bossNames)
        {
            // Find logs specific to this boss
            var logsForBoss = logsByBoss.FirstOrDefault(group => group.Key == boss);

            // If no logs for this boss, skip
            if (logsForBoss == null)
                continue;

            // Find the last log for this boss
            var lastLogForBoss = logsForBoss.OrderByDescending(log => log.TimeStamp).FirstOrDefault();

            if (lastLogForBoss == null)
                continue;

            // Get EntityInfo for the boss
            var infoForBoss = lastLogForBoss.Source.Name == boss
                ? lastLogForBoss.SourceInfo
                : lastLogForBoss.TargetInfo;

            // Accumulate HP values
            totalBossHP += infoForBoss.MaxHP;
            currentBossHP += infoForBoss.CurrentHP;
        }

        return totalBossHP > 0 ? (currentBossHP / totalBossHP) * 100 : 0;
    }
}