namespace SWTORCombatParser.Utilities;

public static class DebugFeatures
{
    public static bool LogReplayEnabled
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}
