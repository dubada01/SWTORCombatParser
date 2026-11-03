using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;


static class CrossPlatformClipboard
{
    public static void SetText(string text)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsClipboard.SetText(text);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            OsxClipboard.SetText(text);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }
}

static class WindowsClipboard
{
    public static void SetText(string text)
    {
        OpenClipboard();

        EmptyClipboard();
        IntPtr hGlobal = default;
        try
        {
            var bytes = (text.Length + 1) * 2;
            hGlobal = Marshal.AllocHGlobal(bytes);

            if (hGlobal == default)
            {
                ThrowWin32();
            }

            var target = GlobalLock(hGlobal);

            if (target == default)
            {
                ThrowWin32();
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
            }
            finally
            {
                GlobalUnlock(target);
            }

            if (SetClipboardData(cfUnicodeText, hGlobal) == default)
            {
                ThrowWin32();
            }

            hGlobal = default;
        }
        finally
        {
            if (hGlobal != default)
            {
                Marshal.FreeHGlobal(hGlobal);
            }

            CloseClipboard();
        }
    }

    public static void OpenClipboard()
    {
        var num = 10;
        while (true)
        {
            if (OpenClipboard(default))
            {
                break;
            }

            if (--num == 0)
            {
                ThrowWin32();
            }

            Thread.Sleep(100);
        }
    }

    const uint cfUnicodeText = 13;

    static void ThrowWin32()
    {
        throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

    [DllImport("user32.dll")]
    static extern bool EmptyClipboard();
}
static class OsxClipboard
{
    public static void SetText(string text)
    {
        var nsString = objc_getClass("NSString");
        IntPtr str = default;
        IntPtr dataType = default;
        try
        {
            str = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), text);
            dataType = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), NSPasteboardTypeString);

            var nsPasteboard = objc_getClass("NSPasteboard");
            var generalPasteboard = objc_msgSend(nsPasteboard, sel_registerName("generalPasteboard"));

            objc_msgSend(generalPasteboard, sel_registerName("clearContents"));
            objc_msgSend(generalPasteboard, sel_registerName("setString:forType:"), str, dataType);
        }
        finally
        {
            if (str != default)
            {
                objc_msgSend(str, sel_registerName("release"));
            }

            if (dataType != default)
            {
                objc_msgSend(dataType, sel_registerName("release"));
            }
        }
    }

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr objc_getClass(string className);

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
    static extern IntPtr sel_registerName(string selectorName);

    const string NSPasteboardTypeString = "public.utf8-plain-text";
}