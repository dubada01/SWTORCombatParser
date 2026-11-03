#if MACOS
using System;
using System.Diagnostics;
using Avalonia;

namespace SWTORCombatParser.Utilities.MouseHandler
{
   public class MouseHookHandler
    {

        public void SubscribeToClicks()
        {
        }

        public void UnsubscribeFromClicks()
        {
        }
        public event Action<Point> MouseClicked = delegate { };
    }
}
#endif
