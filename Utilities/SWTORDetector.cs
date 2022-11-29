using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public static class SwtorDetector
    {
        public static event Action<bool> SwtorProcessStateChanged = delegate { };
        private static bool _monitorForSwtor;
        public static bool SwtorRunning;
        public static void StartMonitoring()
        {
            _monitorForSwtor = true;
            Task.Run(() => {
                while (_monitorForSwtor)
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

                    Thread.Sleep(SwtorRunning ? 500 : 1500);
                }
            });
        }

        private static void UpdateStatus()
        {
            SwtorProcessStateChanged(!SwtorRunning);
        }

        public static void StopMonitoring()
        {
            _monitorForSwtor = false;
        }
    }
}
