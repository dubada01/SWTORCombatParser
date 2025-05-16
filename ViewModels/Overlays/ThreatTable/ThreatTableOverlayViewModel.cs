using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.Views.Overlay.ThreatTable;

namespace SWTORCombatParser.ViewModels.Overlays.ThreatTable;

public class ThreatTableOverlayViewModel :BaseOverlayViewModel
{
    private readonly ThreatTableOverlayView _threatTableView;
    private readonly OverlayInfo _settings;
    private object updateLock = new object();
    private List<ThreatTableEntryViewModel> entryViewModels = new List<ThreatTableEntryViewModel>();
    public ObservableCollection<ThreatTableEntry> ThreatEntries { get; set; } = new ObservableCollection<ThreatTableEntry>();
    public ThreatTableOverlayViewModel(string overlayName) : base(overlayName)
    {
        _threatTableView = new ThreatTableOverlayView(this);
        MainContent = _threatTableView;
        CombatSelectionMonitor.OnInProgressCombatSelected += HandleNewCombatInfo;
        CombatSelectionMonitor.CombatSelected += HandleNewCombatInfo;
        CombatSelectionMonitor.PhaseSelected += HandleNewCombatInfo;
    }

    public void HandleNewCombatInfo(Combat combat)
    {
        // Group by LogId
        var groupedByLogId = combat.PlayerThreatPerEnemy.Keys
            .GroupBy(e => e.LogId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var entityIndexById = new Dictionary<long, int>();
        foreach (var group in groupedByLogId.Values)
        {
            for (int i = 0; i < group.Count; i++)
            {
                entityIndexById[group[i].Id] = i + 1;
            }
        }
        var logIdCountByEntity = groupedByLogId.ToDictionary(g => g.Key, g => g.Value.Count);
        ThreatEntries.Clear();
        entryViewModels.Clear();
        var topDpsEnemies = GetTop3DamageEnemies(combat);
        var enemies = combat.PlayerThreatPerEnemy.Keys
            .Where(k => GetThreatPriorityScore(k, combat,topDpsEnemies) > 0 && (k.IsBoss || !CombatLogStateBuilder.CurrentState.WasEnemyDeadAtTime(k,combat.EndTime)))
            .OrderByDescending(k=>GetThreatPriorityScore(k, combat,topDpsEnemies));
        foreach (var key in enemies)
        {
            if (entryViewModels.Any(e => e.EnemyId == key.Id))
                continue;
            var newEntry = new ThreatTableEntryViewModel(key.Id);
            entryViewModels.Add(newEntry);
        }

        foreach (var entry in entryViewModels)
        {
            entry.UpdateEntry(combat,entityIndexById,logIdCountByEntity);
            Dispatcher.UIThread.Invoke(() =>
            {
                ThreatEntries.Add(new ThreatTableEntry(entry));
            });

        }

    }
    private int GetThreatPriorityScore(Entity enemy, Combat combat, HashSet<Entity> topDpsEnemies)
    {
        int score = 0;

        if (enemy.IsBoss)
            score += 1000;

        var enemyTargetInfo = CombatLogStateBuilder.CurrentState.GetEnemyTargetAtTime(enemy, combat.EndTime);
        if (enemyTargetInfo.Entity.IsLocalPlayer)
            score += 100;

        if (topDpsEnemies.Contains(enemy))
            score += 10;

        return score;
    }
    private HashSet<Entity> GetTop3DamageEnemies(Combat combat)
    {
        return combat.DPS
            .Where(kvp=>!kvp.Key.IsCharacter)
            .OrderByDescending(kvp => kvp.Value)
            .Take(3)
            .Select(kvp => kvp.Key)
            .ToHashSet();
    }

    public override bool ShouldBeVisible => true;

}