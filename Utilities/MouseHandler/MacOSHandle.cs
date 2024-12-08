#if MACOS
using System;
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
