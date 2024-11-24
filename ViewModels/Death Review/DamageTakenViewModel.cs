using System.Linq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.Views.Death_Review;
using SWTORCombatParser.Views.Phases;

namespace SWTORCombatParser.ViewModels.Death_Review;

public class DamageTakenViewModel
{
    private DeathPlotViewModel _deathPlotViewModel;
    public DeathPlot DeathPlotContent { get; set; }
    private PhaseBarViewModel _phaseBarViewModel;
    public PhaseBar PhasebarContent { get; set; }
    
    private DamageTakenBarsViewModel _totalDamageTakenViewModel;
    public DamageTakenBarsView TotalDamageTakenContent { get; set; }
    private DamageTakenBarsViewModel _specificAbilityDamageTakenViewModel;
    private Combat _currentCombat;
    public DamageTakenBarsView SpecificAbilityDamageTakenContent { get; set; }

    public DamageTakenViewModel()
    {
        _deathPlotViewModel = new DeathPlotViewModel();
        DeathPlotContent = new DeathPlot(_deathPlotViewModel);
        
        _phaseBarViewModel = new PhaseBarViewModel();
        PhasebarContent = new PhaseBar(_phaseBarViewModel);
        
        _totalDamageTakenViewModel = new DamageTakenBarsViewModel(BarType.Ability);
        _totalDamageTakenViewModel.OnBarSelected += SelectAbility;
        TotalDamageTakenContent = new DamageTakenBarsView(_totalDamageTakenViewModel);
        
        _specificAbilityDamageTakenViewModel = new DamageTakenBarsViewModel(BarType.Player);
        SpecificAbilityDamageTakenContent = new DamageTakenBarsView(_specificAbilityDamageTakenViewModel);
    }

    private void SelectAbility(BarInfo obj)
    {
        _specificAbilityDamageTakenViewModel.SetAbility(obj.Text,obj.Source);
        _deathPlotViewModel.Reset();
        _deathPlotViewModel.PlotCombat(_currentCombat,_currentCombat.CharacterParticipants,obj.Text, obj.Source);
    }

    public void SetCombat(Combat combat)
    {
        if(combat == null || combat.CharacterParticipants.Count == 0)
        {
            return;
        }

        _currentCombat = combat;
        _totalDamageTakenViewModel.SetCombat(combat);
        _specificAbilityDamageTakenViewModel.SetCombat(combat);
        var abilityDamage = _totalDamageTakenViewModel.GetDamageTakenByAbility();
        if (abilityDamage.Any())
        {
            var mostDamagingAbility = abilityDamage.MaxBy(e => e.Value).Key;
            _specificAbilityDamageTakenViewModel.SetAbility(mostDamagingAbility.AbilityName, mostDamagingAbility.AbilitySource);
            _deathPlotViewModel.Reset();
            _deathPlotViewModel.PlotCombat(combat,combat.CharacterParticipants,mostDamagingAbility.AbilityName, mostDamagingAbility.AbilitySource);
        }
    }
}