using System;
using System.Drawing;
using System.IO;

namespace SWTORCombatParser.Utilities
{
    public static class IconFactory
    {
        private static Bitmap _unknownIcon;

        public static void Init()
        {
            _unknownIcon = new Bitmap(Environment.CurrentDirectory + "/resources/question-mark.png");
        }

        public static Bitmap GetIcon(string className)
        {
            if (string.IsNullOrEmpty(className) || !File.Exists(Environment.CurrentDirectory + $"/resources/Class Icons/{className.ToLower()}.png"))
                return _unknownIcon;
            return new Bitmap(Environment.CurrentDirectory + $"/resources/Class Icons/{className.ToLower()}.png");
        }
    }
}
