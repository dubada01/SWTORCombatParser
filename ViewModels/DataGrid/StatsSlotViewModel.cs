using MvvmHelpers;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Converters;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SWTORCombatParser.ViewModels.DataGrid
{
    public class StatsSlotViewModel:BaseViewModel
    {
        private SolidColorBrush backgroundColor;
        public bool DisplayIcon { get; set; }
        public BitmapSource RoleIcon { get; set; }
        public bool IsLocalPlayer { get; set; }
        public StatsSlotViewModel(OverlayType type, string name = "", string iconName = "", bool isLocalPlayer = false)
        {
            if (!string.IsNullOrEmpty(name))
            {
                IsLocalPlayer = isLocalPlayer;
                if (isLocalPlayer)
                    ForegroundColor = System.Windows.Media.Brushes.Goldenrod;
                else
                    ForegroundColor = System.Windows.Media.Brushes.WhiteSmoke;
                Value = name;
                DisplayIcon = true;
                RoleIcon = IconFactory.GetIcon(iconName);
                return;
            }
            ForegroundColor = (SolidColorBrush)new OverlayMetricToColorConverter().Convert(type, null, null, System.Globalization.CultureInfo.InvariantCulture);
        }
        public string Value { get; set; }
        public SolidColorBrush BackgroundColor
        {
            get => backgroundColor; set
            {
                backgroundColor = value; 
                OnPropertyChanged();
            }
        }
        public SolidColorBrush ForegroundColor { get; set; }
    }
}
