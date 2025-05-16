using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public enum UtilityOverlayType
    {
        RaidHot,
        RaidBoss,
        RaidChallenge,
        RaidTimer,
        DisciplineTimer,
        Personal,
        PvPHP,
        PvPMap, 
        RoomHazard,
        AbilityList,
        RaidNotes,
        Timeline,
        AlertTimer,
        ThreatTable,
        Other
    }
    public class UtilityOverlayOptionViewModel : INotifyPropertyChanged
    {
        private bool isSelected = false;
        private bool enabled = true;
        private string name;
        public string HelpText => Name;
        public string Name { get => name; set 
            { 
                name = value;
                OnPropertyChanged();
            }
        }
        public bool Enabled { get => enabled; set
            { 
                enabled = value;
                OnPropertyChanged();
            }
        }
        public bool IsSelected
        {
            get => isSelected; set
            {
                isSelected = value;
                OnPropertyChanged();
            }
        }
        public UtilityOverlayType Type { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
