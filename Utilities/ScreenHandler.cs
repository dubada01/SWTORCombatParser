using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace SWTORCombatParser.Utilities
{
    public static class ScreenHandler
    {
        public static Screen GetMainScreen()
        {
            return GetScreen(0);
        }
        public static Screen GetCurrentScreen(Window window)
        {
            var parentArea = new Rectangle((int)window.Left, (int)window.Top, (int)window.Width, (int)window.Height);
            return Screen.FromRectangle(parentArea);
        }

        private static Screen GetScreen(int requestedScreen)
        {
            var screens = Screen.AllScreens;
            var mainScreen = 0;
            if (screens.Length > 1 && mainScreen < screens.Length)
            {
                return screens[requestedScreen];
            }
            return screens[0];
        }
    }
}
