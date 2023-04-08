using PInvoke;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.Views;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

namespace SWTORCombatParser
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var threadId = PInvoke.Kernel32.GetCurrentThreadId();
            Verify(PInvoke.User32.SetWindowsHookEx(
                User32.WindowsHookType.WH_CALLWNDPROCRET,
                (code, param, lParam) =>
                {
                    unsafe
                    {
                        ref var info = ref Unsafe.AsRef<PInvoke.User32.CWPRETSTRUCT>((void*)lParam);
                        if (info.message == User32.WindowMessage.WM_GETTEXT)
                        {
                            Marshal.SetLastSystemError(0);
                        }
                        return PInvoke.User32.CallNextHookEx(
                            IntPtr.Zero,
                            code,
                            param,
                            lParam
                        );
                    }
                },
                IntPtr.Zero,
                threadId
            ));

            base.OnStartup(e);
            App_Startup(e);
        }
        private void App_Startup(StartupEventArgs e)
        {
            Process[] processCollection = Process.GetProcesses();
            if (processCollection.Count(pc => pc.ProcessName.ToLower() == "orbs") == 1)
            {
                ConvertToAppData.ConvertFromProgramDataToAppData();
                var task = TimeUtility.StartUpdateTask();
                var mainWindowVM = new MainWindowViewModel();
                var mainWindow = new MainWindow();
                Application.Current.MainWindow = mainWindow;
                mainWindow.DataContext = mainWindowVM;
                mainWindow.Show();
            }
            else
            {
                if (ShouldShowPopup.ReadShouldShowPopup("InstanceRunning"))
                {
                    var warningWindow = new InstanceAlreadyRunningWarning();
                    warningWindow.Show();
                    warningWindow.Closed += (s, e) => { Shutdown(0); };
                }
                else
                {
                    Shutdown(0);
                }
            }
        }
        static T Verify<T>(T handle)
        where T : SafeHandle
        {
            if (!handle.IsInvalid)
                return handle;
            var error = Marshal.GetLastWin32Error();
            if (error == 0)
                return handle;
            throw new Win32Exception(error);
        }
    }
}
