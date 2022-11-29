using System;

namespace SWTORCombatParser.Utilities
{
    public class ParsingPerformanceProfiler
    {
        private DateTime _startTime;
        
        public void StartLogProcessing()
        {
            _startTime = DateTime.Now;
            Logging.LogInfo("Starting to parse logs...");
        }
        public void SaveProcessingInfo()
        {
            Logging.LogInfo("Parsed logs in " + (DateTime.Now - _startTime).TotalSeconds + " seconds.");
        }
    }
}
