#if WINDOWS
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;

namespace SWTORCombatParser.Utilities.MouseHandler;

public class MouseHookHandler
{
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private LowLevelMouseProc _proc;
    private IntPtr _hookID = IntPtr.Zero;
    // Event to fire when a mouse click occurs
    public event Action<Point> MouseClicked = delegate { };
    public void SubscribeToClicks()
    {
        if (_hookID != IntPtr.Zero)
            return;

        _proc = HookCallback;
        _hookID = SetHook(_proc);
        if (_hookID == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not listen for raid frame clicks.");
    }

    public void UnsubscribeFromClicks()
    {
        if (_hookID == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hookID);
        _hookID = IntPtr.Zero;
    }

    public static Point GetCursorPosition()
    {
        return new Point();
    }
    private IntPtr SetHook(LowLevelMouseProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && MouseMessages.WM_LBUTTONDOWN == (int)wParam)
        {
            // Get the click location from lParam
            MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            Point clickLocation = new Point(hookStruct.pt.X, hookStruct.pt.Y);

            // Fire the MouseClicked event
            MouseClicked?.InvokeSafely(clickLocation);
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }
    // Mouse message constants
    private static class MouseMessages
    {
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_MOUSEWHEEL = 0x020A;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
    }

    // Structure for low-level mouse input event data
    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // Win32 POINT is two 32-bit integers. Avalonia.Point uses doubles and
    // changes both the coordinates and the layout of the following fields.
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
    private const int WH_MOUSE_LL = 14;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
#endif
