#if MACOS
using System;
using System.Runtime.InteropServices;
using Avalonia;

namespace SWTORCombatParser.Utilities.MouseHandler
{
    public class MouseHookHandler
    {
        // Event to fire when a mouse click occurs
        public event Action<Point> MouseClicked = delegate { };

        private IntPtr _eventTap;
        private IntPtr _runLoopSource;
        private CFMachPortRef _tapPort;
        private bool _isSubscribed = false;

        public void SubscribeToClicks()
        {
            if (_isSubscribed)
                return;

            _isSubscribed = true;

            // Create the event tap
            _eventTap = CGEventTapCreate(
                CGEventTapLocation.kCGHIDEventTap,
                CGEventTapPlacement.kCGHeadInsertEventTap,
                CGEventTapOptions.kCGEventTapOptionDefault,
                CGEventMask.kCGEventLeftMouseDown,
                MouseEventCallback,0);

            // Ensure the tap is valid
            if (_eventTap == IntPtr.Zero)
            {
                Console.WriteLine("Failed to create event tap.");
                return;
            }

            // Create a RunLoop source and add it to the current RunLoop
            _runLoopSource = CFMachPortCreateRunLoopSource(IntPtr.Zero, _tapPort, 0);
            CFRunLoopAddSource(CFRunLoopGetCurrent(), _runLoopSource, CFRunLoopMode.kCFRunLoopCommonModes);
            CFRunLoopRun();
        }

        public void UnsubscribeFromClicks()
        {
            if (!_isSubscribed)
                return;

            _isSubscribed = false;

            // Remove the event tap from the run loop
            if (_runLoopSource != IntPtr.Zero)
            {
                CFRunLoopRemoveSource(CFRunLoopGetCurrent(), _runLoopSource, CFRunLoopMode.kCFRunLoopCommonModes);
                _runLoopSource = IntPtr.Zero;
            }

            // Disable and release the event tap
            if (_eventTap != IntPtr.Zero)
            {
                CGEventTapEnable(_eventTap, false);
                CFRelease(_eventTap);
                _eventTap = IntPtr.Zero;
            }
        }

        private IntPtr MouseEventCallback(CGEventTapProxy proxy, CGEventType type, IntPtr eventRef, IntPtr userInfo)
        {
            if (type == CGEventType.LeftMouseDown)
            {
                // Get the mouse click position
                var point = CGEventGetLocation(eventRef);

                // Fire the MouseClicked event with the mouse coordinates
                MouseClicked?.Invoke(new Point(point.X, point.Y));
            }

            return eventRef;
        }

        // External methods for interacting with the macOS Event Tap API
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGEventTapCreate(
            CGEventTapLocation tap,
            CGEventTapPlacement place,
            CGEventTapOptions options,
            CGEventMask eventsOfInterest,
            CGEventCallback callback,
            IntPtr userInfo);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern void CGEventTapEnable(IntPtr tap, bool enable);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern CGPoint CGEventGetLocation(IntPtr eventRef);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, CFMachPortRef tap, int order);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, CFRunLoopMode mode);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRunLoopRemoveSource(IntPtr runLoop, IntPtr source, CFRunLoopMode mode);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRunLoopRun();

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFRunLoopGetCurrent();

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr cf);

        // Supporting Enums and Structs
        private enum CGEventTapLocation : uint
        {
            kCGHIDEventTap = 0,
        }

        private enum CGEventTapPlacement : uint
        {
            kCGHeadInsertEventTap = 0,
        }

        private enum CGEventTapOptions : uint
        {
            kCGEventTapOptionDefault = 0,
        }

        private enum CGEventType : uint
        {
            LeftMouseDown = 1,
        }

        private enum CFRunLoopMode
        {
            kCFRunLoopCommonModes,
        }

        private enum CGEventMask : ulong
        {
            kCGEventLeftMouseDown = (1UL << (int)CGEventType.LeftMouseDown),

        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CGEventCallback(CGEventTapProxy proxy, CGEventType type, IntPtr @event, IntPtr refcon);

        private struct CGPoint
        {
            public double X;
            public double Y;
        }

        private struct CFMachPortRef { }
        private struct CGEventTapProxy { }
    }
}
#endif
