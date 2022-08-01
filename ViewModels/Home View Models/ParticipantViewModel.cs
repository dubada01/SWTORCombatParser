using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Home_View_Models
{
    public class ParticipantViewModel : INotifyPropertyChanged
    {
        private bool isSelected;

        public event Action<ParticipantViewModel> SelectionChanged = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public void ToggleSelection()
        {
            IsSelected = !IsSelected;
            if (IsSelected)
                SelectionChanged(this);
        }
        public Entity Entity { get; set; }
        public bool IsSelected { 
            get => isSelected; 
            set 
            { 
                isSelected = value;
                OnPropertyChanged();
            } 
        }
        public string PlayerName { get; set; }
        public bool IsLocalPlayer { get; set; }
        public string RoleImageSource { get; set; }
        public int RoleOrdering { get; set; }
        public string DPS { get; set; } = "0";
        public string HPS { get; set; } = "0";
        public string DTPS { get; set; } = "0";
        public void SetValues(double dps, double hps, double dtps, string roleImage)
        {

            DPS = dps == 0 ? "0" : dps.ToString("#,##");
            OnPropertyChanged("DPS");
            HPS = hps == 0 ? "0" : hps.ToString("#,##");
            OnPropertyChanged("HPS");
            DTPS = dtps == 0 ? "0" : dtps.ToString("#,##");
            OnPropertyChanged("DTPS");
            RoleImageSource = roleImage;
            OnPropertyChanged("RoleImageSource");
        }
    }
}
