using MvvmHelpers;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Converters;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SWTORCombatParser.ViewModels.DataGrid
{
    public class StatsSlotViewModel : BaseViewModel
    {
        private SolidColorBrush backgroundColor;
        public bool DisplayIcon { get; set; }
        public BitmapSource RoleIcon { get; set; }
        public bool IsLocalPlayer { get; set; }
        public bool IsTotal { get; set; }
        public HorizontalAlignment ValueAlignment { get; set; }
        private OverlayType OverlayType { get; set; }
        public StatsSlotViewModel(OverlayType type, System.Windows.Media.Color iconColor, string name = "", string iconName = "", bool isLocalPlayer = false, Entity entity = null)
        {
            OverlayType = type;
            IsTotal = entity == null || name == "Totals";
            if (!string.IsNullOrEmpty(name) && name != "Totals")
            {
                IsLocalPlayer = isLocalPlayer;
                if (isLocalPlayer)
                    ForegroundColor = System.Windows.Media.Brushes.Goldenrod;
                else
                    ForegroundColor = System.Windows.Media.Brushes.WhiteSmoke;
                Value = name;
                DisplayIcon = true;
                var coloredIcon = IconFactory.GetColoredBitmapImage(iconName, iconColor);
                RoleIcon = coloredIcon;
                ValueAlignment = HorizontalAlignment.Center;
                return;
            }
            if (name == "Totals")
            {
                ForegroundColor = System.Windows.Media.Brushes.WhiteSmoke;
                Value = name;
                ValueAlignment = HorizontalAlignment.Center;
                return;
            }
            ValueAlignment = HorizontalAlignment.Right;
            ForegroundColor = (SolidColorBrush)new OverlayMetricToColorConverter().Convert(OverlayType, null, null, System.Globalization.CultureInfo.InvariantCulture);
            MetricColorLoader.OnOverlayTypeColorUpdated += TryUpdateColor;
        }

        private void TryUpdateColor(OverlayType type)
        {
            if(OverlayType == type && Value != "Totals" && DisplayIcon == false)
            {
                ForegroundColor = (SolidColorBrush)new OverlayMetricToColorConverter().Convert(OverlayType, null, null, System.Globalization.CultureInfo.InvariantCulture);
                OnPropertyChanged("ForegroundColor");
            }
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
