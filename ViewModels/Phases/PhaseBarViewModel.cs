using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Views.Phases;
using System;
using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Phases
{
    public class PhaseBarViewModel:ReactiveObject
    {
        public event Action<List<PhaseInstance>> PhaseInstancesUpdated = delegate { };
        public ReactiveCommand<Unit,Unit> ConfigurePhasesCommand { get; }

        public ReactiveCommand<PhaseInstance,Unit> PhaseSelectionToggled { get; }

        private void TogglePhaseSelection(PhaseInstance instance)
        {
            PhaseManager.TogglePhaseInstance(instance);
        }

        public PhaseBarViewModel()
        {
            PhaseManager.PhaseInstancesUpdated += UpdatePhases;
            ConfigurePhasesCommand = ReactiveCommand.Create(ConfigurePhases);
            PhaseSelectionToggled = ReactiveCommand.Create<PhaseInstance>(TogglePhaseSelection);
        }
        private void UpdatePhases(List<PhaseInstance> phases)
        {
            PhaseInstancesUpdated(phases);
        }

        private void ConfigurePhases()
        {
            var vm = new PhaseListViewModel();
            var window = new PhaseListView(vm);
            window.Show();
        }
    }
}
