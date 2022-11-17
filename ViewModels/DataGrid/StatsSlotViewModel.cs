using MvvmHelpers;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities.Converters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
                RoleIcon = ToBitmapImage(new Bitmap(Environment.CurrentDirectory + "/resources/"+ iconName));
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
        public static BitmapImage ToBitmapImage(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
    }
}
