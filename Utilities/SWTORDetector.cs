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
        private static bool _swtorRunning;
        public static void StartMonitoring()
        {
            _monitorForSWTOR = true;
            Task.Run(() => {
                while (_monitorForSWTOR)
                {
                    Process[] processCollection = Process.GetProcesses();
                    if(processCollection.Any(p=>p.ProcessName == "swtor"))
                    {
                        if(!_swtorRunning)
                            UpdateStatus();
                        _swtorRunning = true;
                    }
                    else
                    {
                        if(_swtorRunning)
                            UpdateStatus();
                        _swtorRunning = false;
                    }
                    Thread.Sleep(3000);
                }
            });
        }

        private static void UpdateStatus()
        {
            SWTORProcessStateChanged(!_swtorRunning);
        }

        public static void StopMonitoring()
        {
            _monitorForSWTOR = false;
        }
    }
}
