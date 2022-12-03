using SWTORCombatParser.DataStructures.ClassInfos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SWTORCombatParser.Utilities
{
    public static class IconFactory
    {
        private static BitmapImage _tankIcon;
        private static BitmapImage _healIcon;
        private static BitmapImage _dpsIcon;
        private static BitmapImage _unknownIcon;

        public static void Init()
        {
            _tankIcon = new BitmapImage(new Uri(Environment.CurrentDirectory + "/resources/tankIcon.png"));
            _healIcon = new BitmapImage(new Uri(Environment.CurrentDirectory + "/resources/healingIcon.png"));
            _dpsIcon = new BitmapImage(new Uri(Environment.CurrentDirectory + "/resources/dpsIcon.png"));
            _unknownIcon = new BitmapImage(new Uri(Environment.CurrentDirectory + "/resources/question-mark.png"));
        }
        public static BitmapImage GetIcon(string role)
        {
            switch (role)
            {
                case "tank":
                    return _tankIcon;
                case "healer":
                    return _healIcon;
                case "dps":
                    return _dpsIcon;
                default:
                    return _unknownIcon;
            }
        }
    }
}
