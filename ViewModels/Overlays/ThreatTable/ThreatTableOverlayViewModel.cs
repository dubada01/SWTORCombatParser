using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.Views.Overlay.ThreatTable;

namespace SWTORCombatParser.ViewModels.Overlays.ThreatTable;

public class ThreatTableOverlayViewModel :BaseOverlayViewModel
{
    private readonly ThreatTableOverlayView _threatTableView;
    private readonly OverlayInfo _settings;
    public ObservableCollection<ThreatTableEntry> ThreatEntries { get; set; } = new ObservableCollection<ThreatTableEntry>();
    private List<long> _userAddedIds = new List<long>();
    public ThreatTableOverlayViewModel(string overlayName) : base(overlayName)
    {
        _threatTableView = new ThreatTableOverlayView(this);
        MainContent = _threatTableView;
        CombatSelectionMonitor.OnInProgressCombatSelected += HandleNewCombatInfo;
        CombatSelectionMonitor.CombatSelected += HandleNewCombatInfo;
        CombatSelectionMonitor.PhaseSelected += HandleNewCombatInfo;
        _userAddedIds = Settings.ReadSettingOfType<List<long>>("threat_table_ids");
    }

    public void HandleNewCombatInfo(Combat combat)
    {
        // Live parsing and historical selection arrive on different threads.
        // Serialize the complete update on the UI thread before reading entries;
        // holding a lock while dispatching can deadlock selection publication.
        Dispatcher.UIThread.Invoke(() => UpdateThreatEntries(combat));
    }

    private void UpdateThreatEntries(Combat combat)
    {
        var enemyKeys = combat.PlayerThreatPerEnemy.Keys.ToArray();
        // Group by LogId
        var groupedByLogId = enemyKeys
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
        var topDpsEnemies = GetTop3DamageEnemies(combat);
        var enemies = enemyKeys
            .Where(k => GetThreatPriorityScore(k, combat,topDpsEnemies) > 0)
            .OrderByDescending(k=>GetThreatPriorityScore(k, combat,topDpsEnemies))
            .ToArray();
        foreach (var key in enemies)
        {
            if (ThreatEntries.Any(e => e.ViewModel.EnemyId == key.Id))
            {
                var existingEnemy = ThreatEntries.First(e => e.ViewModel.EnemyId == key.Id);
                if (CombatLogStateBuilder.CurrentState.WasEnemyDeadAtTime(key, combat.EndTime))
                {
                    ThreatEntries.Remove(existingEnemy);
                }
                else
                {
                    existingEnemy.ViewModel.UpdateEntry(combat, entityIndexById, logIdCountByEntity);
                }
                continue;
            }

            if (ThreatEntries.Count >= 3 || CombatLogStateBuilder.CurrentState.WasEnemyDeadAtTime(key, combat.EndTime))
                continue;
            var newEntry = new ThreatTableEntryViewModel(key.Id);
            newEntry.UpdateEntry(combat, entityIndexById, logIdCountByEntity);
            if(!string.IsNullOrEmpty(newEntry.EnemyName))
                ThreatEntries.Add(new ThreatTableEntry(newEntry));
        }

        var allIds = ThreatEntries.Select(v => v.ViewModel.EnemyId).ToList();
        foreach (var existingId in allIds)
        {
            if (enemies.Any(e => e.Id == existingId)) continue;
            {
                var entityToRemove = ThreatEntries.First(e=>e.ViewModel.EnemyId == existingId);
                ThreatEntries.Remove(entityToRemove);
            }
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
        
        if (_userAddedIds.Contains(enemy.LogId))
            score += 800;
        if ((combat.EndTime - combat.LogsInvolvingEntity[enemy.LogId].Last().TimeStamp).TotalSeconds > 10)
            score = 0;
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
