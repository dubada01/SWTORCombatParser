using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace SWTORCombatParser.ViewModels.SoftwareLogging
{
    public class SoftwareLogViewModel
    {
        public ObservableCollection<SoftwareLogInstance> SoftwareLogs { get; set; } = new ObservableCollection<SoftwareLogInstance>();

        internal void AddNewLog(SoftwareLogInstance log)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                SoftwareLogs.Insert(0, log);
            });

        }
    }
}
