#if MACOS
using System;
using Avalonia;

namespace SWTORCombatParser.Utilities.MouseHandler
{
   public class MouseHookHandler
    {

        public void StartListening()
        {
        }

        public void StopListening()
        {
        }
        public event Action<Point> MouseClicked = delegate { };
    }
}
#endif
