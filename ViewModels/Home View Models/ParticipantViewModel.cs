using SWTORCombatParser.DataStructures;
using System;
using System.Globalization;
using Avalonia.Media.Imaging;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Home_View_Models
{
    public class ParticipantViewModel : ReactiveObject
    {
        private bool isSelected;
        private bool diedNatrually;
        private string hPPercentText;
        private double hPPercent;
        private Bitmap _roleImageSource;
        private string _dps = "0";
        private string _hps = "0";
        private string _dtps = "0";

        public event Action<ParticipantViewModel, bool> SelectionChanged = delegate { };
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
                this.RaiseAndSetIfChanged(ref isSelected, value);
            }
        }
        public string PlayerName { get; set; }
        public bool IsLocalPlayer { get; set; }

        public Bitmap RoleImageSource
        {
            get => _roleImageSource;
            set => this.RaiseAndSetIfChanged(ref _roleImageSource, value);
        }

        public int RoleOrdering { get; set; }

        public string DPS
        {
            get => _dps;
            set => this.RaiseAndSetIfChanged(ref _dps, value);
        }

        public string HPS
        {
            get => _hps;
            set => this.RaiseAndSetIfChanged(ref _hps, value);
        }

        public string DTPS
        {
            get => _dtps;
            set => this.RaiseAndSetIfChanged(ref _dtps, value);
        }

        public double HPPercent
        {
            get => hPPercent; set
            {
                this.RaiseAndSetIfChanged(ref hPPercent, value);

                HPPercentText = Math.Round(HPPercent * 100, 2).ToString(CultureInfo.InvariantCulture) + "%";
            }
        }
        public string HPPercentText
        {
            get => hPPercentText; set
            {
                this.RaiseAndSetIfChanged(ref hPPercentText, value);
            }
        }
        public bool DiedNatrually
        {
            get => diedNatrually; set
            {
                this.RaiseAndSetIfChanged(ref diedNatrually, value);
            }
        }
        public void SetValues(double dps, double hps, double dtps, Bitmap roleImage)
        {
            DPS = dps == 0 ? "0" : dps.ToString("#,##", CultureInfo.InvariantCulture);
            HPS = hps == 0 ? "0" : hps.ToString("#,##", CultureInfo.InvariantCulture);
            DTPS = dtps == 0 ? "0" : dtps.ToString("#,##", CultureInfo.InvariantCulture);
            RoleImageSource = roleImage;
        }
    }
}
