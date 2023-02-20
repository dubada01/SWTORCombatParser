using System;
using System.Windows;
using System.Windows.Media;

namespace SWTORCombatParser.Utilities
{
    public static class ResourceFinder
    {
        public static Color GetColorFromResourceName(string keyName)
        {
            var myResourceDictionary = new ResourceDictionary();
            myResourceDictionary.Source =
                new Uri("/Utilities/UIStyles/Colors.xaml",
                        UriKind.RelativeOrAbsolute);
            return (Color)myResourceDictionary[keyName];
        }
    }
}
