using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MvvmHelpers;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Converters;


namespace SWTORCombatParser.ViewModels.DataGrid
{
    public class StatsSlotViewModel : ReactiveObject
    {
        private SolidColorBrush backgroundColor;
        private Bitmap _roleIcon;
        public bool DisplayIcon { get; set; }
        public string Header { get; set; }
        public Bitmap RoleIcon
        {
            get => _roleIcon;
            set => this.RaiseAndSetIfChanged(ref _roleIcon, value);
        }

        public bool IsLocalPlayer { get; set; }
        public bool IsTotal { get; set; }
        public HorizontalAlignment ValueAlignment { get; set; }
        private OverlayType OverlayType { get; set; }
        public StatsSlotViewModel(OverlayType type, Color iconColor, string name = "", string iconName = "", bool isLocalPlayer = false, Entity entity = null)
        {
            Header = new OverlayTypeToReadableNameConverter().Convert(type,null,null,System.Globalization.CultureInfo.InvariantCulture).ToString();
            OverlayType = type;
            IsTotal = entity == null || name == "Totals";
            if (!string.IsNullOrEmpty(name) && name != "Totals")
            {
                Header = "Name";
                IsLocalPlayer = isLocalPlayer;
                // if (isLocalPlayer)
                //     ForegroundColor = new SolidColorBrush(Brushes.Goldenrod.Color);
                // else
                //     ForegroundColor =  new SolidColorBrush(Brushes.WhiteSmoke.Color);
                // Value = name;
                DisplayIcon = true;
                var coloredIcon = IconFactory.GetColoredBitmapImage(iconName, iconColor);
                RoleIcon = coloredIcon;
                ValueAlignment = HorizontalAlignment.Center;
                return;
            }
            if (name == "Totals")
            {
                //ForegroundColor =  new SolidColorBrush(Brushes.WhiteSmoke.Color);
                Value = name;
                ValueAlignment = HorizontalAlignment.Center;
                return;
            }
            ValueAlignment = HorizontalAlignment.Right;
            ForegroundColor = (SolidColorBrush)new OverlayMetricToColorConverter().Convert(OverlayType, null, null, System.Globalization.CultureInfo.InvariantCulture);
            //MetricColorLoader.OnOverlayTypeColorUpdated += TryUpdateColor;
        }
        
        public string Value { get; set; }
        // public SolidColorBrush BackgroundColor
        // {
        //     get => backgroundColor; set
        //     {
        //         backgroundColor = value;
        //         OnPropertyChanged();
        //     }
        // }


        public SolidColorBrush ForegroundColor { get; set; }
    }
}
