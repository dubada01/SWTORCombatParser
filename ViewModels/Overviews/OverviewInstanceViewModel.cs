using SWTORCombatParser.DataStructures;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public abstract class OverviewInstanceViewModel : ReactiveObject
    {
        internal OverviewDataType _type;
        internal Entity _selectedEntity;
        public abstract SortingOption SortingOption { get; set; }
        public OverviewInstanceViewModel(OverviewDataType type)
        {
            _type = type;
        }
        public void UpdateEntity(Entity selectedEntity)
        {
            _selectedEntity = selectedEntity;
            UpdateParticipant();
        }
        public Combat SelectedCombat { get; set; }
        public abstract void UpdateData(Combat combat);

        internal abstract void UpdateParticipant();
        internal abstract void Update();

        public abstract void Reset();

    }
}
