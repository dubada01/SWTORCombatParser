using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.BattleReview;
using SWTORCombatParser.Views.Battle_Review;

namespace SWTORCombatParser.ViewModels.Death_Review;

public class EffectAndStack
{
    public string EffectName { get; set; }
    public int EffectStacks { get; set; }
    public Bitmap Icon { get; set; }
}
public class TenSecondRecapViewModel:ReactiveObject
{    
    private EventHistoryViewModel _deathLogsViewModel;
    private double _currentSliderValue;
    private Combat _currentCombat;
    private Entity _selectedBoss;
    private Entity _selectedPlayer;
    private List<Entity> _inScopeBosses = new List<Entity>();
    private List<EffectAndStack> _selectedPlayerDebuffs;
    private List<EffectAndStack> _selectedBossBuffs;
    private List<Entity> _inScopePlayers = new List<Entity>();
    private List<Entity> _availableBosses;
    private readonly string _allPlayers = "All Players";
    private readonly string _allBosses = "All Bosses";

    private int _timeOffset = 30;
    private DateTime _currentSelectedTime = DateTime.MinValue;
    private List<Entity> _availablePlayers;

    public string CurrentTimeOffset => (-_timeOffset * (1-_currentSliderValue)).ToString("N2");
    public double CurrentSliderValue
    {
        get => _currentSliderValue;
        set
        {
            if(_currentCombat == null)
                return;
            _currentSelectedTime = _currentCombat.EndTime.AddSeconds(-_timeOffset * (1-value));
            this.RaiseAndSetIfChanged(ref _currentSliderValue, value);
            this.RaisePropertyChanged(nameof(CurrentTimeOffset));
            UpdateBuffsAndDebuffs();
            _deathLogsViewModel.Seek((_currentSelectedTime - _currentCombat.StartTime).TotalSeconds);
        }
    }
    
    // PLAYER INFO

    public Entity SelectedPlayer
    {
        get => _selectedPlayer;
        set
        {
            if(value == null)
                return;
            this.RaiseAndSetIfChanged(ref _selectedPlayer, value);
            _inScopePlayers = _selectedPlayer.Name == _allPlayers ? _currentCombat.CharacterParticipants : new List<Entity>() { _selectedPlayer };
            _ = RefreshInScopeEntities();
            UpdateBuffsAndDebuffs();
        }
    }

    public List<Entity> AvailablePlayers
    {
        get => _availablePlayers;
        set => this.RaiseAndSetIfChanged(ref _availablePlayers, value);
    }

    public List<EffectAndStack> SelectedPlayerDebuffs
    {
        get => _selectedPlayerDebuffs;
        set => this.RaiseAndSetIfChanged(ref _selectedPlayerDebuffs, value);
    }
    
    
    // BOSS INFO
    public Entity SelectedBoss
    {
        get => _selectedBoss;
        set
        {            
            if(value == null)
                return;
            this.RaiseAndSetIfChanged(ref _selectedBoss, value);
            _inScopeBosses = _selectedBoss.Name == _allBosses ? _currentCombat.AllEntities.Where(e => e.IsBoss).ToList() : new List<Entity>() { _selectedBoss };
            _ = RefreshInScopeEntities();
            UpdateBuffsAndDebuffs();
        }
    }

    public List<Entity> AvailableBosses
    {
        get => _availableBosses;
        set => this.RaiseAndSetIfChanged(ref _availableBosses, value);
    }

    public List<EffectAndStack> SelectedBossBuffs
    {
        get => _selectedBossBuffs;
        set => this.RaiseAndSetIfChanged(ref _selectedBossBuffs, value);
    }

    public EventHistoryView DeathLogsView { get; set; }


    public TenSecondRecapViewModel()
    {
        _deathLogsViewModel = new EventHistoryViewModel();
        _deathLogsViewModel.SetDisplayType(DisplayType.DeathRecap);
        DeathLogsView = new EventHistoryView(_deathLogsViewModel);
    }
    public void SetCombat(Combat combat)
    {        
        if(combat.AllLogs.Count == 0)
            return;
        Task.Run(async () =>
        {
            _currentCombat = combat;
            _currentSelectedTime = combat.EndTime.AddSeconds(-_timeOffset);
            CurrentSliderValue = 0;
        
            var players = _currentCombat.CharacterParticipants.ToList();
            players.Insert(0, new Entity() { Name = _allPlayers });
            AvailablePlayers = players;
        
            var bosses = _currentCombat.AllEntities.Where(e => e.IsBoss).ToList();
            bosses.Insert(0, new Entity() { Name = _allBosses });
            AvailableBosses = bosses;
        
            SelectedBoss = AvailableBosses.First();
            SelectedPlayer = AvailablePlayers.First();
        
            await _deathLogsViewModel.SelectCombat(combat);
            await _deathLogsViewModel.UpdateLogs(true);
        });
    }
    
    private void UpdateBuffsAndDebuffs()
    {
        if (SelectedPlayer != null && SelectedPlayer.Name != _allPlayers)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                SelectedPlayerDebuffs = await GetDefuffsForPlayer(SelectedPlayer);
            });
            
        }
        if (SelectedBoss != null && SelectedBoss.Name != _allBosses)
        {           
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                SelectedBossBuffs = await GetBuffsForBoss(SelectedBoss);
            });
        }
    }

    private async Task<List<EffectAndStack>> GetDefuffsForPlayer(Entity selectedPlayer)
    {
        var allEffectsOnPlayer = GetEffectsOnEntityAtTime(selectedPlayer);
        var debuffs = allEffectsOnPlayer.Where((e => !e.Source.IsCharacter));
        var effectsWithIcons = await Task.WhenAll(
            debuffs.Select(async d => new EffectAndStack
            {
                Icon = await IconGetter.GetIconForId(d.EffectId),
                EffectName = d.EffectName,
                EffectStacks = d.GetEffectStackForTimestamp(_currentSelectedTime)
            })
        );

        return effectsWithIcons.ToList();
    }
    private async Task<List<EffectAndStack>> GetBuffsForBoss(Entity selectedBoss)
    {
        var allEffectsOnBoss = GetEffectsOnEntityAtTime(selectedBoss);
        var buffs = allEffectsOnBoss.Where((e => !e.Source.IsCharacter));
        var effectsWithIcons = await Task.WhenAll(
            buffs.Select(async d => new EffectAndStack
            {
                Icon = await IconGetter.GetIconForId(d.EffectId),
                EffectName = d.EffectName,
                EffectStacks = d.GetEffectStackForTimestamp(_currentSelectedTime)
            })
        );

        return effectsWithIcons.ToList();
    }
    private List<CombatModifier> GetEffectsOnEntityAtTime(Entity entity)
    {
        var allEffectsOnEntity = CombatLogStateBuilder.CurrentState.GetEffectsWithTarget(_currentSelectedTime,entity);
        return allEffectsOnEntity;
    }
    private async Task RefreshInScopeEntities()
    {
        var allEntities = _inScopeBosses.Concat(_inScopePlayers).Where(e=>e.Name != _allPlayers && e.Name != _allBosses).ToList();
        _deathLogsViewModel.SetViewableEntities(allEntities);
        await _deathLogsViewModel.UpdateLogs(true);
    }
}