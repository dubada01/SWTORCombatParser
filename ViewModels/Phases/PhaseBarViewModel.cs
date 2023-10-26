using Prism.Commands;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.Views.Phases;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Phases
{
    public class PhaseBarViewModel
    {
        public event Action<List<PhaseInstance>> PhaseInstancesUpdated = delegate { };
        public ICommand ConfigurePhasesCommand => new DelegateCommand(ConfigurePhases);

        public ICommand PhaseSelectionToggled => new DelegateCommand<PhaseInstance>(TogglePhaseSelection);

        private void TogglePhaseSelection(PhaseInstance instance)
        {
            PhaseManager.TogglePhaseInstance(instance);
        }

        public PhaseBarViewModel()
        {
            PhaseManager.PhaseInstancesUpdated += UpdatePhases;
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
