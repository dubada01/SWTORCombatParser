using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Death_Review;

public class BarInfo:ReactiveObject
{
    private SolidColorBrush _barColor = (SolidColorBrush)Application.Current.Resources["ParticipantDTPSBrush"];
    private bool _isSelected;
    public string Text { get; set; }
    public Bitmap Icon { get; set; }
    public double Value { get; set; }
    public string SourceName { get; set; }
    public double Ratio { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            BarColor = _isSelected ? (SolidColorBrush)Application.Current.Resources["HeaderTextBrush"] : (SolidColorBrush)Application.Current.Resources["ParticipantDTPSBrush"];
        }
    }

    public SolidColorBrush BarColor
    {
        get => _barColor;
        set => this.RaiseAndSetIfChanged(ref _barColor, value);
    }

    public Entity Source { get; set; }
}
public enum BarType
{
    Ability,
    Player
}
public class DamageTakenBarsViewModel:ReactiveObject
{
    private BarType _barType;
    private Combat _currentCombat;
    private string _currentPlayer = "All Players";
    private string _currentAbility;
    
    private Dictionary<RichAbility,double> _abilityDamageTaken = new Dictionary<RichAbility, double>();
    private Dictionary<Entity,double> _playerDamageTaken = new Dictionary<Entity, double>();
    private List<BarInfo> _barInfo = new List<BarInfo>();
    private string _titleString;
    private Entity _currentSource;

    public event Action<BarInfo> OnBarSelected = delegate { };

    public DamageTakenBarsViewModel(BarType barType)
    {
        _barType = barType;
        switch (barType)
        {
            case BarType.Ability:
                TitleString = "Raidwide Damage Taken";
                break;
        }
    }

    public void SetPlayer(string playerName)
    {
        _currentPlayer = playerName;
        UpdateBars();
    }

    public void SetAbility(string abilityString, Entity sourceEntity)
    {
        _currentSource = sourceEntity;
        _currentAbility = abilityString;
        TitleString = abilityString;
        UpdateBars();
    }
    public Dictionary<RichAbility,double> GetDamageTakenByAbility()
    {
        return _abilityDamageTaken;
    }
    public Dictionary<Entity,double> GetDamageTakenByPlayer()
    {
        return _playerDamageTaken;
    }

    public string TitleString
    {
        get => _titleString;
        set => this.RaiseAndSetIfChanged(ref _titleString, value);
    }

    public List<BarInfo> BarInfo
    {
        get => _barInfo;
        set => this.RaiseAndSetIfChanged(ref _barInfo, value);
    }

    public void SetCombat(Combat combat)
    {
        _currentCombat = combat;
        UpdateBars();
    }

    private void UpdateBars()
    {
        switch(_barType)
        {
            case BarType.Ability:
                UpdateAbilityBars();
                break;
            case BarType.Player:
                UpdatePlayerBars();
                break;
        }
    }

    private void UpdatePlayerBars()
    {
        BarInfo = new List<BarInfo>();
        if(string.IsNullOrEmpty(_currentAbility))
            return;
        _playerDamageTaken = new Dictionary<Entity, double>();
        foreach (var player in _currentCombat.CharacterParticipants)
        {
            _playerDamageTaken[player] =  _currentSource.IsCharacter ?  
                (_currentCombat.GetDamageIncomingByAbilityForPlayer(_currentAbility, player)/_currentCombat.DurationSeconds) :  
                (_currentCombat.GetDamageIncomingByAbilityForPlayerFromSource(_currentAbility, player,_currentSource)/_currentCombat.DurationSeconds);
        }
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_playerDamageTaken == null || !_playerDamageTaken.Any())
            {
                BarInfo = new List<BarInfo>(); // Clear the UI list if no data is available
                return;
            }

            var maxValue = _playerDamageTaken.Values.Max();

            // Use Task.WhenAll to load icons in parallel
            var barInfoTasks = _playerDamageTaken.Select(async e => new BarInfo()
            {
                Text = e.Key.Name,
                Icon = IconFactory.GetClassIcon(CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(e.Key, _currentCombat.StartTime).Discipline),
                Value = e.Value,
                Ratio = e.Value / maxValue
            });

            var barInfoList = await Task.WhenAll(barInfoTasks);

            // Update the UI-bound property
            BarInfo = barInfoList
                .OrderByDescending(b => b.Ratio)
                .ToList();
        });

    }

    private void UpdateAbilityBars()
    {
        BarInfo = new List<BarInfo>();
        _abilityDamageTaken = new Dictionary<RichAbility, double>(new RichAbilityComparer());
        if (_currentPlayer == "All Players")
        {
            foreach(var player in _currentCombat.CharacterParticipants)
            {
                var abilityResults = _currentCombat.GetIncomingDamageByAbilityRich(player);
                foreach(var ability in abilityResults)
                {
                    if(_abilityDamageTaken.ContainsKey(ability.Key))
                        _abilityDamageTaken[ability.Key] += (ability.Value.Sum(e=>e.Value.EffectiveDblValue)/_currentCombat.DurationSeconds);
                    else
                        _abilityDamageTaken[ability.Key] = (ability.Value.Sum(e=>e.Value.EffectiveDblValue)/_currentCombat.DurationSeconds);
                }
            }
        }
        else
        {
            var player = _currentCombat.CharacterParticipants.First(e=>e.Name == _currentPlayer);
            var abilityResults = _currentCombat.GetIncomingDamageByAbilityRich(player);
            foreach(var ability in abilityResults)
            {
                if(_abilityDamageTaken.ContainsKey(ability.Key))
                    _abilityDamageTaken[ability.Key] += ability.Value.Sum(e=>e.Value.EffectiveDblValue);
                else
                    _abilityDamageTaken[ability.Key] = ability.Value.Sum(e=>e.Value.EffectiveDblValue);
            }
        }
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_abilityDamageTaken == null || !_abilityDamageTaken.Any())
            {
                BarInfo = new List<BarInfo>(); // Clear the UI list if no data is available
                return;
            }

            var maxValue = _abilityDamageTaken.Values.Max();

            // Use Task.WhenAll to load icons in parallel
            var barInfoTasks = _abilityDamageTaken.Select(async e => new BarInfo()
            {
                Text = e.Key.AbilityName,
                Icon = await IconGetter.GetIconForId(e.Key.AbilityId),
                SourceName = e.Key.AbilitySource.IsCharacter ? "" : e.Key.AbilitySource.Name,
                Source = e.Key.AbilitySource,
                Value = e.Value,
                IsSelected = e.Value == maxValue,
                Ratio = e.Value / maxValue
            });

            var barInfoList = await Task.WhenAll(barInfoTasks);

            // Update the UI-bound property
            BarInfo = barInfoList
                .OrderByDescending(b => b.Ratio)
                .ToList();
        });


    }

    public void BarSelected(BarInfo barInfo)
    {
        if(_barType == BarType.Player)
            return;
        foreach (var bar in BarInfo)
        {
            bar.IsSelected = false;
        }

        barInfo.IsSelected = true;
        OnBarSelected(barInfo);
    }
}