using System;
using Avalonia.Controls;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.Death_Review;

namespace SWTORCombatParser.ViewModels.Death_Review;

public sealed class RaidwideBattleReviewViewModel : ReactiveObject, IDisposable
{
    private readonly Combat _combat;
    private readonly UserControl?[] _tabs = new UserControl?[3];
    private int _selectedTabIndex;
    private TenSecondRecapViewModel? _recap;
    private DeathReviewViewModel? _deaths;

    public RaidwideBattleReviewViewModel(Combat combat) => _combat = combat;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (value < 0 || value >= _tabs.Length || value == _selectedTabIndex) return;
            this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
            this.RaisePropertyChanged(nameof(CurrentReviewContent));
        }
    }

    public UserControl CurrentReviewContent => _tabs[SelectedTabIndex] ??= CreateTab(SelectedTabIndex);
    public string EncounterName => _combat.EncounterBossDifficultyParts.Item1;
    public string CompletionText => $"{(_combat.WasBossKilled ? "Kill" : "No clear")}  ·  {TimeSpan.FromSeconds(_combat.DurationSeconds):mm\\:ss}";

    private UserControl CreateTab(int index)
    {
        switch (index)
        {
            case 0:
                var damage = new DamageTakenViewModel();
                damage.SetCombat(_combat);
                return new DamageTakenView(damage);
            case 1:
                _recap = new TenSecondRecapViewModel();
                _recap.SetCombat(_combat);
                return new TenSecondRecapView(_recap);
            default:
                _deaths = new DeathReviewViewModel();
                _deaths.AddCombat(_combat);
                return new DeathReviewPage(_deaths);
        }
    }

    public void Dispose()
    {
        _recap?.Dispose();
        _deaths?.Dispose();
    }
}
