using System;
using Avalonia;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities
{
    public static class ResourceFinder
    {
        public static Color GetColorFromResourceName(string keyName)
        {
            // Assuming that the resources are already loaded in Application.Current.Resources
            if (Application.Current.Resources.TryGetValue(keyName, out var resource))
            {
                if (resource is Color color)
                {
                    return color;
                }
            }

            throw new ArgumentException($"Color resource with key '{keyName}' not found.");
        }
    }
}
