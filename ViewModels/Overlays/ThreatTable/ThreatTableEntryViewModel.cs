using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.ViewModels.Overlays.ThreatTable;

public class ThreatTableEntryViewModel(long keyId) :ReactiveObject
{

    private string _topPlayerName;
    private IImmutableSolidColorBrush _topPlayerColor;
    private double _topPlayerThreat;
    private string _secondPlayerName;
    private IImmutableSolidColorBrush _secondPlayerColor;
    private double _secondPlayerThreat;
    private string _enemyName;
    private bool _topIsLocal;
    private bool _bottomIsLocal;
    private bool _topIsTank;
    private bool _bottomIsTank;
    
    public long EnemyId = keyId;
    private IImmutableSolidColorBrush _remainderColor;
    private bool _isCurrentLocalTarget;
    private bool _enemyIsBoss;
    public IImmutableSolidColorBrush EnemyBackground => IsCurrentLocalTarget ? Brushes.SteelBlue : Brushes.DimGray;
    public IImmutableSolidColorBrush EnemyNameColor => EnemyIsBoss ? Brushes.Goldenrod : Brushes.WhiteSmoke;
    public bool IsCurrentLocalTarget
    {
        get => _isCurrentLocalTarget;
        set => this.RaiseAndSetIfChanged(ref _isCurrentLocalTarget, value);
    }

    public bool EnemyIsBoss
    {
        get => _enemyIsBoss;
        set => this.RaiseAndSetIfChanged(ref _enemyIsBoss, value);
    }

    public string EnemyName
    {
        get => _enemyName;
        set => this.RaiseAndSetIfChanged(ref _enemyName, value);
    }
    public bool TopIsTank {get => _topIsTank; set => this.RaiseAndSetIfChanged(ref _topIsTank, value);  }    
    public bool TopIsLocal { get => _topIsLocal; set => this.RaiseAndSetIfChanged(ref _topIsLocal, value); }
    public FontWeight TopFontWeight => TopIsLocal ? FontWeight.Bold : FontWeight.Normal;
    public string TopPlayerName
    {
        get => _topPlayerName;
        set => this.RaiseAndSetIfChanged(ref _topPlayerName, value);
    }

    public IImmutableSolidColorBrush TopPlayerColor
    {
        get => _topPlayerColor;
        set => this.RaiseAndSetIfChanged(ref _topPlayerColor, value);
    }

    public double TopPlayerThreat
    {
        get => _topPlayerThreat;
        set => this.RaiseAndSetIfChanged(ref _topPlayerThreat, value);
    }
    public bool BottomIstank {get => _bottomIsTank; set => this.RaiseAndSetIfChanged(ref _bottomIsTank, value); }

    public bool BottomIsLocal { get => _bottomIsLocal; set => this.RaiseAndSetIfChanged(ref _bottomIsLocal, value); }
    public FontWeight BottomFontWeight => BottomIsLocal ? FontWeight.Bold : FontWeight.Normal;
    public string SecondPlayerName
    {
        get => _secondPlayerName;
        set => this.RaiseAndSetIfChanged(ref _secondPlayerName, value);
    }
    
    public IImmutableSolidColorBrush SecondPlayerColor
    {
        get => _secondPlayerColor;
        set => this.RaiseAndSetIfChanged(ref _secondPlayerColor, value);
    }

    public double SecondPlayerThreat
    {
        get => _secondPlayerThreat;
        set => this.RaiseAndSetIfChanged(ref _secondPlayerThreat, value);
    }
    public GridLength ColumnRatio => new GridLength(_ratio, GridUnitType.Star);
    public GridLength RemainderRatio => new GridLength(1-_ratio, GridUnitType.Star);

    public IImmutableSolidColorBrush RemainderColor
    {
        get => _remainderColor;
        set => this.RaiseAndSetIfChanged(ref _remainderColor, value);
    }

    private double DeltaThreat => TopPlayerThreat - SecondPlayerThreat;
    private double _ratio => TopPlayerThreat == 0 ? 0 : SecondPlayerThreat / TopPlayerThreat;
    
    

    public void UpdateEntry(Combat fullCombat, Dictionary<long, int> entityIndexById, Dictionary<long, int> logIdCountByEntity)
    {
        TopIsTank = false;
        BottomIstank = false;
        TopIsLocal = false;
        BottomIsLocal = false;
        var entity = fullCombat.AllEntities.FirstOrDefault(e => e.Id == EnemyId);
        if (entity == null)
            return;
        EnemyIsBoss = entity.IsBoss;
        if (entityIndexById.TryGetValue(EnemyId, out int index) &&
            logIdCountByEntity.TryGetValue(entity.LogId, out int count) && count > 1)
        {
            EnemyName = $"{entity.Name} {index}";
        }
        else
        {
            EnemyName = entity.Name;
        }

        var entityThreatInfo = fullCombat.PlayerThreatPerEnemy[entity];
        var topTwo = entityThreatInfo.OrderByDescending(kvp => kvp.Value).Take(2).ToList();

        if (topTwo.Count > 0)
        {
            TopPlayerName = topTwo[0].Key.Name;
            TopPlayerThreat = topTwo[0].Value;
            if(topTwo[0].Key.IsLocalPlayer)
                TopIsLocal = true;
            if(topTwo.Count > 1 && topTwo[1].Key.IsLocalPlayer)
                BottomIsLocal = true;
            if (CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(topTwo[0].Key, fullCombat.EndTime).Role ==
                Role.Tank)
            {
                TopIsTank = true;
            }

            if (topTwo.Count > 1 && CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(topTwo[1].Key, fullCombat.EndTime).Role ==
                Role.Tank)
            {
                BottomIstank = true;
            }

            if (topTwo.Count > 1)
            {
                SecondPlayerName = topTwo[1].Key.Name;
                SecondPlayerThreat = topTwo[1].Value;
            }
            else
            {
                SecondPlayerName = "";
                SecondPlayerThreat = 0;
            }
        }

        if (double.IsNaN(_ratio) || double.IsInfinity(_ratio))
            TopPlayerThreat = 1;


        if (_topIsTank)
        {
            double t = Math.Clamp((_ratio - 0.5) / 0.5, 0, 1); // 0 at 0.5, 1 at 1.0
            RemainderColor = InterpolateColor(t, Colors.DarkGreen, Colors.DarkRed);
        }
        else if (_bottomIsTank)
        {
            RemainderColor = Brushes.DarkRed;
        }
        else
        {
            RemainderColor = Brushes.DimGray;
        }
       
        IsCurrentLocalTarget = CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(CombatLogStateBuilder.CurrentState.LocalPlayer, fullCombat.EndTime).Entity.Id == EnemyId;
        var playerTargetName = CombatLogStateBuilder.CurrentState
            .GetPlayerTargetAtTime(CombatLogStateBuilder.CurrentState.LocalPlayer, fullCombat.EndTime).Entity.Name;
        Debug.WriteLine($"Current Target: {playerTargetName} {(IsCurrentLocalTarget?"Matches":"DoesntMatch")} {EnemyId} with name {EnemyName}");
        this.RaisePropertyChanged(nameof(EnemyBackground));
        this.RaisePropertyChanged(nameof(ColumnRatio));
        this.RaisePropertyChanged(nameof(RemainderRatio));
        this.RaisePropertyChanged(nameof(RemainderColor));
        this.RaisePropertyChanged(nameof(TopFontWeight));
        this.RaisePropertyChanged(nameof(BottomFontWeight));
        this.RaisePropertyChanged(nameof(EnemyNameColor));
    }

    private static ImmutableSolidColorBrush InterpolateColor(double t, Color start, Color end)
    {
        byte r = (byte)(start.R + (end.R - start.R) * t);
        byte g = (byte)(start.G + (end.G - start.G) * t);
        byte b = (byte)(start.B + (end.B - start.B) * t);

        return new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));
    }
}