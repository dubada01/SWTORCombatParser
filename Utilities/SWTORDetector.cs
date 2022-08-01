using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public static class SWTORDetector
    {
        public static event Action<bool> SWTORProcessStateChanged = delegate { };
        private static bool _monitorForSWTOR;
        public static bool SwtorRunning;
        public static void StartMonitoring()
        {
            _monitorForSWTOR = true;
            Task.Run(() => {
                while (_monitorForSWTOR)
                {
                    Process[] processCollection = Process.GetProcesses();
                    if(processCollection.Any(p=>p.ProcessName == "swtor"))
                    {
                        if(!SwtorRunning)
                            UpdateStatus();
                        SwtorRunning = true;
                    }
                    else
                    {
                        if(SwtorRunning)
                            UpdateStatus();
                        SwtorRunning = false;
                    }
                    if(SwtorRunning)
                        Thread.Sleep(500);
                    else
                        Thread.Sleep(1500);
                }
            });
        }

        private static void UpdateStatus()
        {
            SWTORProcessStateChanged(!SwtorRunning);
        }

        public static void StopMonitoring()
        {
            _monitorForSWTOR = false;
        }
    }
}
