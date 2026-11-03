using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Views.Phases;
using System;
using System.Collections.Generic;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using SWTORCombatParser.Utilities;

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
            PhaseInstancesUpdated.InvokeSafely(phases);
        }

        private void ConfigurePhases()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var vm = new PhaseListViewModel();
                var window = new PhaseListView(vm);
                window.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner;
                window.Show(desktop.MainWindow);
            }
        }
    }
}
