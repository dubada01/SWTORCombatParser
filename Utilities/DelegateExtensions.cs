using System;

namespace SWTORCombatParser.Utilities;

public static class DelegateExtensions
{
    public static void InvokeSafely(this Action eventDelegate)
    {
        if (eventDelegate == null)
            return;
        foreach (var handler in eventDelegate.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception ex)
            {
                Logging.LogError($"Exception in event handler: {ex.Message} \r\n {ex.StackTrace}");
            }
        }
    }

    public static void InvokeSafely<T>(this Action<T> eventDelegate, T arg)
    {
        if (eventDelegate == null)
            return;
        foreach (var handler in eventDelegate.GetInvocationList())
        {
            try
            {
                ((Action<T>)handler)(arg);
            }
            catch (Exception ex)
            {
                Logging.LogError($"Exception in event handler: {ex.Message} \r\n {ex.StackTrace}");
            }
        }
    }

    public static void InvokeSafely<T1, T2>(this Action<T1, T2> eventDelegate, T1 arg1, T2 arg2)
    {
        if (eventDelegate == null)
            return;
        foreach (var handler in eventDelegate.GetInvocationList())
        {
            try
            {
                ((Action<T1, T2>)handler)(arg1, arg2);
            }
            catch (Exception ex)
            {
                Logging.LogError($"Exception in event handler: {ex.Message} \r\n {ex.StackTrace}");
            }
        }
    }
    public static void InvokeSafely<T1, T2,T3>(this Action<T1, T2,T3> eventDelegate, T1 arg1, T2 arg2, T3 arg3)
    {
        if (eventDelegate == null)
            return;
        foreach (var handler in eventDelegate.GetInvocationList())
        {
            try
            {
                ((Action<T1, T2,T3>)handler)(arg1, arg2,arg3);
            }
            catch (Exception ex)
            {
                Logging.LogError($"Exception in event handler: {ex.Message} \r\n {ex.StackTrace}");
            }
        }
    }
}