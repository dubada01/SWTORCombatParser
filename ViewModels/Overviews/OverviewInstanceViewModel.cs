using SWTORCombatParser.DataStructures;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public abstract class OverviewInstanceViewModel : INotifyPropertyChanged
    {
        internal OverviewDataType _type;
        internal Entity _selectedEntity;
        public event PropertyChangedEventHandler PropertyChanged;
        public abstract SortingOption SortingOption { get; set; }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
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
