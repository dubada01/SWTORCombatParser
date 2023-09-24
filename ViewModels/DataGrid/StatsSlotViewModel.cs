using MvvmHelpers;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Converters;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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
        public StatsSlotViewModel(OverlayType type, System.Windows.Media.Color iconColor, string name = "", string iconName = "", bool isLocalPlayer = false)
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
                var coloredIcon = SetIconColor(IconFactory.GetIcon(iconName), iconColor);
                RoleIcon = coloredIcon;

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

        private BitmapImage SetIconColor(Bitmap image, System.Windows.Media.Color color)
        {
            Rectangle rect = new Rectangle(0, 0, image.Width, image.Height);
            BitmapData bmpData = image.LockBits(rect, ImageLockMode.ReadWrite, image.PixelFormat);

            IntPtr ptr = bmpData.Scan0;

            int bytes = bmpData.Stride * image.Height;
            byte[] rgbValues = new byte[bytes];
            Marshal.Copy(ptr, rgbValues, 0, bytes);
            for (int y = 0; y < bmpData.Height; y++)
            {
                for (int x = 0; x < bmpData.Width; x++)
                {
                    if (rgbValues[(y * bmpData.Stride) + (x * 4)] != 0)
                    {
                        rgbValues[(y * bmpData.Stride) + (x * 4)] = color.B;
                        rgbValues[(y * bmpData.Stride) + (x * 4) + 1] = color.G;
                        rgbValues[(y * bmpData.Stride) + (x * 4) + 2] = color.R;
                    }
                }
            }
            Marshal.Copy(rgbValues, 0, bmpData.Scan0, bytes);
            image.UnlockBits(bmpData);

            return BitmapToImageSource(image);
        }
        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
        public SolidColorBrush ForegroundColor { get; set; }
    }
}
