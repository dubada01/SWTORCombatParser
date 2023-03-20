using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.ViewModels.Home_View_Models
{
    public class ParticipantViewModel : INotifyPropertyChanged
    {
        private bool isSelected;
        private bool diedNatrually;
        private string hPPercentText;
        private double hPPercent;

        public event Action<ParticipantViewModel, bool> SelectionChanged = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public void ToggleSelection()
        {
            IsSelected = !IsSelected;
            SelectionChanged(this, IsSelected);
        }
        public Entity Entity { get; set; }
        public bool IsSelected
        {
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
        public double HPPercent
        {
            get => hPPercent; set
            {
                hPPercent = value;
                OnPropertyChanged();

                HPPercentText = Math.Round(HPPercent * 100, 2) + "%";
            }
        }
        public string HPPercentText
        {
            get => hPPercentText; set
            {
                hPPercentText = value;
                OnPropertyChanged();
            }
        }
        public bool DiedNatrually
        {
            get => diedNatrually; set
            {
                diedNatrually = value;
                OnPropertyChanged();
            }
        }
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
